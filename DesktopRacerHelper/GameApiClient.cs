using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopRacerHelper;

internal sealed class GameApiClient
{
	public const int ExpectedProtocol = 3;

	public const string ExpectedPluginVersion = "1.0.2";

	private const int MaxResponseBytes = 8388608;

	private const int FirstPluginPort = 19110;

	private const int LastPluginPort = 19120;

	private static readonly object DiscoveryLock = new object();

	private static readonly Dictionary<int, GameApiRuntimeStatus> DiscoveredStatuses = new Dictionary<int, GameApiRuntimeStatus>();

	private static readonly Dictionary<int, DateTimeOffset> LastDiscoveryAt = new Dictionary<int, DateTimeOffset>();

	private static readonly HashSet<int> DiscoveryInProgress = new HashSet<int>();

	private static readonly TimeSpan DiscoveryThrottle = TimeSpan.FromMilliseconds(750.0);

	private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromMilliseconds(700.0);

	private static readonly TimeSpan DiscoveryCacheLifetime = TimeSpan.FromSeconds(4.0);

	private static int _expectedProcessId;

	private static string StatusPath => Path.Combine(Deployment.Folder, "game-api-status.json");

	public GameApiRuntimeStatus? RuntimeStatus => ReadRuntimeStatus();

	public static GameApiRuntimeStatus? ReadRuntimeStatus()
	{
		return ReadRuntimeStatus(Volatile.Read(ref _expectedProcessId));
	}

	public static void SetExpectedProcessId(int processId)
	{
		Interlocked.Exchange(ref _expectedProcessId, Math.Max(0, processId));
	}

	public static GameApiRuntimeStatus? ReadRuntimeStatus(int expectedProcessId)
	{
		GameApiRuntimeStatus? fileStatus = ReadRuntimeStatusFile(out int preferredPort);
		if (fileStatus != null && (expectedProcessId <= 0 || fileStatus.HostPid == expectedProcessId))
		{
			CacheDiscoveredStatus(fileStatus);
			return fileStatus;
		}
		GameApiRuntimeStatus? cached = GetCachedStatus(expectedProcessId);
		if (cached != null && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - cached.HeartbeatUnixMs <= DiscoveryCacheLifetime.TotalMilliseconds)
		{
			return cached;
		}
		QueueRuntimeProbe(expectedProcessId, preferredPort);
		return cached;
	}

	private static GameApiRuntimeStatus? ReadRuntimeStatusFile(out int preferredPort)
	{
		preferredPort = 0;
		try
		{
			if (!File.Exists(StatusPath))
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(StatusPath));
			JsonElement rootElement = jsonDocument.RootElement;
			GameApiRuntimeStatus gameApiRuntimeStatus = new GameApiRuntimeStatus(GetString(rootElement, "plugin_version"), GetInt(rootElement, "protocol"), GetInt(rootElement, "host_pid"), GetInt(rootElement, "port"), GetString(rootElement, "engine_version"), GetString(rootElement, "game_version"), GetBool(rootElement, "kernel_ready"), GetBool(rootElement, "authenticated"), GetString(rootElement, "connection_state"), GetBool(rootElement, "connected"), GetLong(rootElement, "heartbeat_unix_ms", 0L));
			preferredPort = gameApiRuntimeStatus.Port;
			return IsRuntimeStatusUsable(gameApiRuntimeStatus) ? gameApiRuntimeStatus : null;
		}
		catch
		{
			return null;
		}
	}

	private static bool IsRuntimeStatusUsable(GameApiRuntimeStatus status)
	{
		if (status.Protocol != ExpectedProtocol || status.PluginVersion != ExpectedPluginVersion || status.HostPid <= 0 || status.Port < 1 || status.Port > 65535 || status.HeartbeatUnixMs <= 0)
		{
			return false;
		}
		long age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - status.HeartbeatUnixMs;
		if (age < -5000 || age > 6000)
		{
			return false;
		}
		try
		{
			using Process process = Process.GetProcessById(status.HostPid);
			return !process.HasExited;
		}
		catch
		{
			return false;
		}
	}

	private static void CacheDiscoveredStatus(GameApiRuntimeStatus status)
	{
		lock (DiscoveryLock)
		{
			DiscoveredStatuses[status.HostPid] = status;
		}
	}

	private static GameApiRuntimeStatus? GetCachedStatus(int expectedProcessId)
	{
		lock (DiscoveryLock)
		{
			if (expectedProcessId > 0)
			{
				if (DiscoveredStatuses.TryGetValue(expectedProcessId, out GameApiRuntimeStatus? expected) && IsRuntimeStatusUsable(expected))
				{
					return expected;
				}
				DiscoveredStatuses.Remove(expectedProcessId);
				return null;
			}
			foreach (int processId in DiscoveredStatuses.Keys.ToArray())
			{
				if (!IsRuntimeStatusUsable(DiscoveredStatuses[processId]))
				{
					DiscoveredStatuses.Remove(processId);
				}
			}
			return DiscoveredStatuses.Values.OrderByDescending(status => status.HeartbeatUnixMs).FirstOrDefault();
		}
	}

	private static void QueueRuntimeProbe(int expectedProcessId, int preferredPort)
	{
		int probeKey = expectedProcessId > 0 ? expectedProcessId : 0;
		lock (DiscoveryLock)
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			if (DiscoveryInProgress.Contains(probeKey)
				|| (LastDiscoveryAt.TryGetValue(probeKey, out DateTimeOffset last) && now - last < DiscoveryThrottle))
			{
				return;
			}
			DiscoveryInProgress.Add(probeKey);
			LastDiscoveryAt[probeKey] = now;
		}
		_ = Task.Run(async () =>
		{
			try
			{
				GameApiRuntimeStatus? discovered = await ProbeLoopbackAsync(expectedProcessId, preferredPort);
				lock (DiscoveryLock)
				{
					if (discovered != null)
					{
						DiscoveredStatuses[discovered.HostPid] = discovered;
					}
				}
			}
			finally
			{
				lock (DiscoveryLock)
				{
					DiscoveryInProgress.Remove(probeKey);
				}
			}
		});
	}

	private static async Task<GameApiRuntimeStatus?> ProbeLoopbackAsync(int expectedProcessId, int preferredPort)
	{
		List<int> ports = new List<int>();
		if (preferredPort >= FirstPluginPort && preferredPort <= LastPluginPort)
		{
			ports.Add(preferredPort);
		}
		for (int port = FirstPluginPort; port <= LastPluginPort; port++)
		{
			if (!ports.Contains(port))
			{
				ports.Add(port);
			}
		}
		foreach (int port in ports)
		{
			GameApiRuntimeStatus? status = await ProbePortAsync(port);
			if (status != null && (expectedProcessId <= 0 || status.HostPid == expectedProcessId))
			{
				return status;
			}
		}
		return null;
	}

	private static async Task<GameApiRuntimeStatus?> ProbePortAsync(int port)
	{
		GameApiRuntimeStatus route = new GameApiRuntimeStatus("", 0, 0, port, "", "", false, false, "", false, 0L);
		try
		{
			JsonElement response = await RequestAsync(route, new
			{
				id = Guid.NewGuid().ToString("N"),
				op = "ping"
			}, DiscoveryTimeout, CancellationToken.None);
			if (!GetBool(response, "ok") || !response.TryGetProperty("data", out JsonElement data))
			{
				return null;
			}
			GameApiRuntimeStatus status = new GameApiRuntimeStatus(GetString(data, "plugin_version"), GetInt(data, "protocol"), GetInt(data, "host_pid"), GetInt(data, "port"), GetString(data, "engine_version"), GetString(data, "game_version"), GetBool(data, "kernel_ready"), GetBool(data, "authenticated"), GetString(data, "connection_state"), GetBool(data, "connected"), GetLong(data, "heartbeat_unix_ms", 0L));
			return status.Port == port && IsRuntimeStatusUsable(status) ? status : null;
		}
		catch
		{
			return null;
		}
	}

	public async Task<GameApiRuntimeStatus> PingAsync(CancellationToken token = default(CancellationToken))
	{
		GameApiRuntimeStatus status = ReadRuntimeStatus() ?? throw new InvalidOperationException("游戏接口尚未就绪或心跳已过期。");
		JsonElement value = await RequestAsync(status, new
		{
			id = Guid.NewGuid().ToString("N"),
			op = "ping"
		}, TimeSpan.FromSeconds(5.0), token);
		if (!GetBool(value, "ok"))
		{
			throw new InvalidOperationException("游戏内 API 心跳失败：" + GetString(value, "error"));
		}
		return status;
	}

	public async Task<JsonElement> GetSnapshotAsync(CancellationToken token = default(CancellationToken))
	{
		JsonElement response = await RequestAsync(ReadRuntimeStatus() ?? throw new InvalidOperationException("游戏接口尚未就绪或心跳已过期。"), new
		{
			id = Guid.NewGuid().ToString("N"),
			op = "snapshot"
		}, TimeSpan.FromSeconds(10.0), token);
		EnsureSuccess(response);
		return response.GetProperty("data").GetProperty("snapshot").Clone();
	}

	public async Task<IReadOnlyList<GameApiEvent>> GetEventsAsync(long afterSequence, CancellationToken token = default(CancellationToken))
	{
		JsonElement response = await RequestAsync(ReadRuntimeStatus() ?? throw new InvalidOperationException("游戏接口尚未就绪或心跳已过期。"), new
		{
			id = Guid.NewGuid().ToString("N"),
			op = "events",
			after = afterSequence
		}, TimeSpan.FromSeconds(5.0), token);
		EnsureSuccess(response);
		JsonElement property = response.GetProperty("data").GetProperty("events");
		List<GameApiEvent> list = new List<GameApiEvent>(property.GetArrayLength());
		foreach (JsonElement item in property.EnumerateArray())
		{
			list.Add(new GameApiEvent(GetLong(item, "sequence", 0L), GetString(item, "event_id"), GetLong(item, "opened_at_unix_ms", 0L), item.TryGetProperty("item", out var value) ? value.Clone() : default(JsonElement)));
		}
		return list;
	}

	public async Task<PluginActionResponse> SendActionAsync(string action, object payload, CancellationToken token = default(CancellationToken))
	{
		JsonElement value = await RequestAsync(ReadRuntimeStatus() ?? throw new InvalidOperationException("游戏接口尚未就绪或心跳已过期。"), new
		{
			id = Guid.NewGuid().ToString("N"),
			op = "action",
			action = action,
			payload = payload
		}, TimeSpan.FromSeconds(125.0), token);
		if (!GetBool(value, "ok"))
		{
			return new PluginActionResponse(Ok: false, GetString(value, "error"), default, default, default);
		}
		JsonElement property = value.GetProperty("data");
		JsonElement value2;
		return new PluginActionResponse(GetBool(property.GetProperty("completed"), "ok"), GetString(property.GetProperty("completed"), "reason"), property.GetProperty("completed").Clone(), property.TryGetProperty("item_obtained", out value2) ? value2.Clone() : default(JsonElement), property.GetProperty("snapshot").Clone());
	}

	private static async Task<JsonElement> RequestAsync(GameApiRuntimeStatus status, object request, TimeSpan timeout, CancellationToken token)
	{
		using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
		timeoutCts.CancelAfter(timeout);
		using TcpClient client = new TcpClient(AddressFamily.InterNetwork)
		{
			NoDelay = true
		};
		_ = 4;
		try
		{
			await client.ConnectAsync(IPAddress.Loopback, status.Port, timeoutCts.Token);
			JsonElement result;
			await using (NetworkStream stream = client.GetStream())
			{
				byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + "\n");
				await stream.WriteAsync(bytes, timeoutCts.Token);
				await stream.FlushAsync(timeoutCts.Token);
				using MemoryStream output = new MemoryStream();
				byte[] buffer = new byte[8192];
				int num2;
				while (true)
				{
					int num = await stream.ReadAsync(buffer, timeoutCts.Token);
					if (num == 0)
					{
						throw new IOException("游戏内 API 提前关闭了连接。");
					}
					if (output.Length + num > 8388608)
					{
						throw new InvalidDataException("游戏内 API 响应超过大小上限。");
					}
					num2 = Array.IndexOf(buffer, (byte)10, 0, num);
					if (num2 >= 0)
					{
						break;
					}
					output.Write(buffer, 0, num);
				}
				output.Write(buffer, 0, num2);
				using JsonDocument jsonDocument = JsonDocument.Parse((ReadOnlyMemory<byte>)output.ToArray(), default(JsonDocumentOptions));
				result = jsonDocument.RootElement.Clone();
			}
			return result;
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			throw new TimeoutException("等待游戏内 API 返回结果超时；本次动作不会自动重发。 ");
		}
	}

	private static void EnsureSuccess(JsonElement response)
	{
		if (!GetBool(response, "ok"))
		{
			throw new InvalidOperationException("游戏内 API 请求失败：" + GetString(response, "error"));
		}
	}

	internal static string GetString(JsonElement value, string key, string fallback = "")
	{
		if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var value2))
		{
			return fallback;
		}
		if (value2.ValueKind != JsonValueKind.String)
		{
			return value2.ToString();
		}
		return value2.GetString() ?? fallback;
	}

	internal static int GetInt(JsonElement value, string key, int fallback = 0)
	{
		if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var value2) || value2.ValueKind != JsonValueKind.Number)
		{
			return fallback;
		}
		if (value2.TryGetInt32(out var value3))
		{
			return value3;
		}
		if (value2.TryGetDouble(out var value4) && double.IsFinite(value4) && value4 >= -2147483648.0 && value4 <= 2147483647.0 && Math.Truncate(value4) == value4)
		{
			return (int)value4;
		}
		return fallback;
	}

	internal static long GetLong(JsonElement value, string key, long fallback = 0L)
	{
		if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var value2) || value2.ValueKind != JsonValueKind.Number)
		{
			return fallback;
		}
		if (value2.TryGetInt64(out var value3))
		{
			return value3;
		}
		if (value2.TryGetDouble(out var value4) && double.IsFinite(value4) && value4 >= -9.223372036854776E+18 && value4 <= 9.223372036854776E+18 && Math.Truncate(value4) == value4)
		{
			return (long)value4;
		}
		return fallback;
	}

	internal static bool GetBool(JsonElement value, string key, bool fallback = false)
	{
		if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var value2))
		{
			JsonValueKind valueKind = value2.ValueKind;
			bool flag = valueKind - 5 <= JsonValueKind.Object;
			return flag ? value2.GetBoolean() : fallback;
		}
		return fallback;
	}

	internal static double GetDouble(JsonElement value, string key, double fallback = 0.0)
	{
		if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var value2) || value2.ValueKind != JsonValueKind.Number)
		{
			return fallback;
		}
		if (!value2.TryGetDouble(out var value3) || !double.IsFinite(value3))
		{
			return fallback;
		}
		return value3;
	}
}
