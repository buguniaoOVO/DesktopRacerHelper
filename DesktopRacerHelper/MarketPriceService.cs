using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopRacerHelper;

internal sealed class MarketPriceService
{
	private const int GameAppId = 5033210;

	private const int MinimumRequestIntervalSeconds = 2;

	private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromSeconds(1.0);

	private static readonly TimeSpan PriceTtl = TimeSpan.FromMinutes(15.0);

	private static readonly TimeSpan MissingTtl = TimeSpan.FromMinutes(5.0);

	private static readonly HttpClient Http = CreateClient();

	private static readonly SemaphoreSlim RequestQueue = new SemaphoreSlim(1, 1);

	private static DateTime _nextRequestUtc = DateTime.MinValue;

	private static DateTime _blockedUntilUtc = DateTime.MinValue;

	private readonly Dictionary<string, MarketPriceQuote> _cache;

	private static string CachePath => Path.Combine(Path.GetDirectoryName(AppSettings.SettingsPath), "steam-price-cache.json");

	public MarketPriceService()
	{
		_cache = LoadCache();
	}

	public MarketPriceQuote? Cached(ItemEntry item, int currencyCode = 23)
	{
		string key = CacheKey(item, currencyCode);
		if (_cache.TryGetValue(key, out MarketPriceQuote value))
		{
			return value;
		}
		string hash = MarketHashName(item);
		string prefix = $"{5033210}|{currencyCode}|";
		return _cache.Values.FirstOrDefault((MarketPriceQuote quote) => quote.CacheKey.StartsWith(prefix, StringComparison.Ordinal) && string.Equals(quote.MarketHashName, hash, StringComparison.OrdinalIgnoreCase));
	}

	public bool IsStale(MarketPriceQuote quote)
	{
		return !IsFresh(quote);
	}

	public async Task<MarketPriceQuote> QueryAsync(ItemEntry item, AppSettings settings, CancellationToken token = default(CancellationToken))
	{
		string key = CacheKey(item, settings.MarketCurrencyCode);
		string marketName = MarketHashName(item);
		if (_cache.TryGetValue(key, out MarketPriceQuote cached) && IsFresh(cached))
		{
			return cached;
		}
		string cachePrefix = $"{5033210}|{settings.MarketCurrencyCode}|";
		MarketPriceQuote marketPriceQuote = _cache.Values.FirstOrDefault((MarketPriceQuote quote) => quote.CacheKey.StartsWith(cachePrefix, StringComparison.Ordinal) && string.Equals(quote.MarketHashName, marketName, StringComparison.OrdinalIgnoreCase) && IsFresh(quote));
		if ((object)marketPriceQuote != null)
		{
			MarketPriceQuote marketPriceQuote2 = marketPriceQuote with
			{
				CacheKey = key
			};
			_cache[key] = marketPriceQuote2;
			SaveCache();
			return marketPriceQuote2;
		}
		await RequestQueue.WaitAsync(token);
		try
		{
			if (_cache.TryGetValue(key, out cached) && IsFresh(cached))
			{
				return cached;
			}
			if (DateTime.UtcNow < _blockedUntilUtc)
			{
				return CacheOrFailure(cached, key, marketName, "rate-limited", "Steam 暂时限制了价格查询，请稍后再试。");
			}
			if (_nextRequestUtc > DateTime.UtcNow)
			{
				await Task.Delay(_nextRequestUtc - DateTime.UtcNow, token);
			}
			token.ThrowIfCancellationRequested();
			_nextRequestUtc = DateTime.UtcNow.AddSeconds(MinimumRequestIntervalSeconds);
			string requestUri = "https://steamcommunity.com/market/priceoverview/?" + $"appid={5033210}&currency={settings.MarketCurrencyCode}&market_hash_name={Uri.EscapeDataString(marketName)}";
			using HttpResponseMessage response = await GetPriceOverviewAsync(requestUri, token);
			if (response.StatusCode == HttpStatusCode.TooManyRequests)
			{
				_blockedUntilUtc = DateTime.UtcNow.AddSeconds(60.0);
				return CacheOrFailure(cached, key, marketName, "rate-limited", "Steam 暂时限制了价格查询，请 60 秒后再试。");
			}
			response.EnsureSuccessStatusCode();
			MarketPriceQuote result;
			await using (Stream body = await response.Content.ReadAsStreamAsync(token))
			{
				CancellationToken cancellationToken = token;
				using JsonDocument jsonDocument = await JsonDocument.ParseAsync(body, default, cancellationToken);
				JsonElement rootElement = jsonDocument.RootElement;
				bool success = rootElement.TryGetProperty("success", out var value) && value.ValueKind == JsonValueKind.True;
				string lowestPrice = ReadString(rootElement, "lowest_price");
				string medianPrice = ReadString(rootElement, "median_price");
				string volume = ReadString(rootElement, "volume");
				bool hasPrice = success && (!string.IsNullOrWhiteSpace(lowestPrice) || !string.IsNullOrWhiteSpace(medianPrice));
				MarketPriceQuote marketPriceQuote3 = new MarketPriceQuote(key, marketName, hasPrice, lowestPrice, medianPrice, volume, DateTime.Now, hasPrice ? "ok" : "no-listing");
				_cache[key] = marketPriceQuote3;
				SaveCache();
				result = marketPriceQuote3;
			}
			return result;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			return CacheOrFailure(cached, key, marketName, "error", ex2.Message);
		}
		finally
		{
			RequestQueue.Release();
		}
	}

	public static string MarketHashName(ItemEntry item)
	{
		string canonicalName = ItemCatalog.Correct(item.Name.Trim());
		return Regex.Replace(canonicalName, "\\s*\\[\\s*M\\s*\\]\\s*[lI1|]?\\s*$", "", RegexOptions.IgnoreCase).Trim();
	}

	private static string CacheKey(ItemEntry item, int currencyCode)
	{
		string value = GameVision.Key(MarketHashName(item));
		return $"{5033210}|{currencyCode}|{value}|{item.Quality}";
	}

	private static string ReadString(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return value.GetString() ?? "";
	}

	private static async Task<HttpResponseMessage> GetPriceOverviewAsync(string requestUri, CancellationToken token)
	{
		for (int attempt = 0; ; attempt++)
		{
			try
			{
				HttpResponseMessage response = await Http.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, token);
				if (attempt == 0 && (int)response.StatusCode >= 500)
				{
					response.Dispose();
					await Task.Delay(TransientRetryDelay, token);
					continue;
				}
				return response;
			}
			catch (HttpRequestException) when (attempt == 0)
			{
				await Task.Delay(TransientRetryDelay, token);
			}
			catch (OperationCanceledException) when (attempt == 0 && !token.IsCancellationRequested)
			{
				await Task.Delay(TransientRetryDelay, token);
			}
		}
	}

	private static bool IsFresh(MarketPriceQuote quote)
	{
		TimeSpan timeSpan = (quote.Available ? PriceTtl : MissingTtl);
		return DateTime.Now - quote.FetchedAt < timeSpan;
	}

	private static MarketPriceQuote CacheOrFailure(MarketPriceQuote? cached, string key, string marketName, string status, string error)
	{
		if ((object)cached != null)
		{
			return cached with
			{
				Status = status + ": " + error
			};
		}
		return new MarketPriceQuote(key, marketName, Available: false, "", "", "", DateTime.Now, status + ": " + error);
	}

	private Dictionary<string, MarketPriceQuote> LoadCache()
	{
		try
		{
			if (!File.Exists(CachePath))
			{
				return new Dictionary<string, MarketPriceQuote>(StringComparer.OrdinalIgnoreCase);
			}
			return JsonSerializer.Deserialize<Dictionary<string, MarketPriceQuote>>(File.ReadAllText(CachePath)) ?? new Dictionary<string, MarketPriceQuote>(StringComparer.OrdinalIgnoreCase);
		}
		catch
		{
			return new Dictionary<string, MarketPriceQuote>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private void SaveCache()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
			string text = CachePath + ".tmp";
			File.WriteAllText(text, JsonSerializer.Serialize(_cache, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			File.Move(text, CachePath, overwrite: true);
		}
		catch
		{
		}
	}

	private static HttpClient CreateClient()
	{
		HttpClient client = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(15.0),
		};
		client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DesktopRacerHelper", "0.7.18"));
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		client.DefaultRequestHeaders.Referrer = new Uri("https://steamcommunity.com/market/");
		client.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
		return client;
	}
}
