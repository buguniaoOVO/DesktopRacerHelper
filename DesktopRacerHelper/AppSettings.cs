using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopRacerHelper;

internal sealed class AppSettings
{
	public int DelayMinSeconds { get; set; } = 5;

	public int DelayMaxSeconds { get; set; } = 10;

	public int ConnectionCheckSeconds { get; set; } = 2;

	public bool WaitForReconnect { get; set; } = true;

	public int WarehouseCheckMinutes { get; set; } = 10;

	public int WarehouseThresholdPercent { get; set; } = 80;

	public int ChestCheckMinutes { get; set; } = 5;

	public bool RepairEnabled { get; set; } = true;

	public int RepairCheckMinutes { get; set; } = 10;

	public int RepairThresholdPercent { get; set; } = 80;

	public bool CollectSupplyBoxesAfterSort { get; set; } = true;

	public bool SortAfterSupplyBoxClaim { get; set; } = true;

	public int MarketCurrencyCode { get; set; } = 23;

	public string Language { get; set; } = "zh";

	public bool CloseToTray { get; set; }

	public bool DryRun { get; set; } = true;

	public bool SkipNameMarker { get; set; } = true;

	public int FavoritePasses { get; set; } = 1;

	public bool AutoRunEnabled { get; set; }

	public bool AntiDropEnabled { get; set; }

	public int AntiDropCheckMinutes { get; set; } = 3;

	public string GameExePath { get; set; } = "";

	public int SteamAppId { get; set; } = 5033210;

	public List<QualityRule> QualityRules { get; set; } = CreateDefaultRules();

	[JsonIgnore]
	public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopRacerHelper", "settings.json");

	private static List<QualityRule> CreateDefaultRules()
	{
		return Theme.Qualities.Select((QualityStyle q) => new QualityRule(q.Name)).ToList();
	}

	public static AppSettings Load()
	{
		try
		{
			if (!File.Exists(SettingsPath))
			{
				return new AppSettings();
			}
			AppSettings appSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
			if (appSettings == null)
			{
				return new AppSettings();
			}
			appSettings.QualityRules = MergeRules(appSettings.QualityRules);
			appSettings.DelayMinSeconds = Math.Clamp(appSettings.DelayMinSeconds, 2, 3600);
			appSettings.DelayMaxSeconds = Math.Clamp(appSettings.DelayMaxSeconds, appSettings.DelayMinSeconds, 3600);
			appSettings.MarketCurrencyCode = Math.Clamp(appSettings.MarketCurrencyCode, 1, 36);
			appSettings.AntiDropCheckMinutes = Math.Clamp((appSettings.AntiDropCheckMinutes <= 0) ? 3 : appSettings.AntiDropCheckMinutes, 1, 60);
			appSettings.RepairCheckMinutes = Math.Clamp((appSettings.RepairCheckMinutes <= 0) ? 10 : appSettings.RepairCheckMinutes, 1, 240);
			appSettings.RepairThresholdPercent = Math.Clamp((appSettings.RepairThresholdPercent <= 0) ? 80 : appSettings.RepairThresholdPercent, 1, 100);
			if (appSettings.SteamAppId <= 0)
			{
				appSettings.SteamAppId = 5033210;
			}
			appSettings.FavoritePasses = 1;
			appSettings.SkipNameMarker = true;
			return appSettings;
		}
		catch
		{
			return new AppSettings();
		}
	}

	private static List<QualityRule> MergeRules(List<QualityRule>? stored)
	{
		List<QualityRule> list = new List<QualityRule>();
		foreach (QualityStyle style in Theme.Qualities)
		{
			QualityRule qualityRule = stored?.FirstOrDefault((QualityRule r) => string.Equals(r.Quality, style.Name, StringComparison.Ordinal));
			list.Add((qualityRule == null) ? new QualityRule(style.Name) : new QualityRule(style.Name)
			{
				Favorite = qualityRule.Favorite,
				Dismantle = qualityRule.Dismantle,
				Sell = qualityRule.Sell
			});
		}
		return list;
	}

	public QualityRule RuleOf(string quality)
	{
		return QualityRules.FirstOrDefault((QualityRule r) => string.Equals(r.Quality, quality, StringComparison.Ordinal)) ?? new QualityRule(quality);
	}

	public void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
		string contents = JsonSerializer.Serialize(this, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		string text = SettingsPath + ".tmp";
		File.WriteAllText(text, contents);
		File.Move(text, SettingsPath, overwrite: true);
	}
}
