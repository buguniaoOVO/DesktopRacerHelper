using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopRacerHelper;

internal sealed class ChestLog
{
	private const int MaxRecords = 400;

	private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _seenGameEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	public List<ChestRecord> Records { get; set; } = new List<ChestRecord>();

	public List<ChestRecord> TradeRecords { get; set; } = new List<ChestRecord>();

	public List<string> KnownItems { get; set; } = new List<string>();

	public List<DateTime> RecentOpenedAt { get; set; } = new List<DateTime>();

	public long TotalOpened { get; set; }

	public Dictionary<string, long> QualityTotals { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

	public long TotalDrops => TotalOpened;

	public bool DropCountsNormalized { get; set; }

	public int OpenedLast24Hours => RecentOpenedAt.Count((DateTime time) => time >= DateTime.Now.AddHours(-24.0));

	[JsonIgnore]
	public string? LoadWarning { get; private set; }

	[JsonIgnore]
	public string? LastSaveError { get; private set; }

	private static string Path => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AppSettings.SettingsPath), "chest-log.json");

	private static string BackupPath => Path + ".bak";

	public static ChestLog Load()
	{
		ChestLog chestLog = null;
		Exception ex = null;
		string text = "";
		if (File.Exists(Path))
		{
			try
			{
				chestLog = Read(Path);
			}
			catch (Exception ex2)
			{
				ex = ex2;
				PreserveCorruptFile();
			}
		}
		if (chestLog == null && File.Exists(BackupPath))
		{
			try
			{
				chestLog = Read(BackupPath);
				File.Copy(BackupPath, Path, overwrite: true);
				text = ((ex == null) ? "主统计文件缺失，已从备份恢复历史数据。" : "主统计文件无法读取，已从备份恢复历史数据；损坏文件已另存。");
			}
			catch (Exception ex3)
			{
				text = "统计文件和备份均无法读取：" + ex3.Message + "。原文件已保留。";
			}
		}
		if (chestLog == null)
		{
			chestLog = new ChestLog();
			if (ex != null)
			{
				text = "统计文件无法读取，已保留损坏文件：" + ex.Message + "。没有覆盖原文件。";
			}
			chestLog.LoadWarning = ((text.Length == 0) ? null : text);
			return chestLog;
		}
		ChestLog chestLog2 = chestLog;
		if (chestLog2.Records == null)
		{
			List<ChestRecord> list = (chestLog2.Records = new List<ChestRecord>());
		}
		chestLog2 = chestLog;
		if (chestLog2.TradeRecords == null)
		{
			List<ChestRecord> list = (chestLog2.TradeRecords = new List<ChestRecord>());
		}
		chestLog2 = chestLog;
		if (chestLog2.KnownItems == null)
		{
			List<string> list4 = (chestLog2.KnownItems = new List<string>());
		}
		chestLog2 = chestLog;
		if (chestLog2.RecentOpenedAt == null)
		{
			List<DateTime> list6 = (chestLog2.RecentOpenedAt = new List<DateTime>());
		}
		chestLog.QualityTotals = new Dictionary<string, long>(chestLog.QualityTotals ?? new Dictionary<string, long>(), StringComparer.OrdinalIgnoreCase);
		foreach (string knownItem in chestLog.KnownItems)
		{
			if (!string.IsNullOrWhiteSpace(knownItem))
			{
				chestLog._seen.Add(knownItem);
			}
		}
		foreach (string item in chestLog.Records.Select((ChestRecord record) => record.EventId))
		{
			if (!string.IsNullOrWhiteSpace(item))
			{
				chestLog._seenGameEvents.Add(item);
			}
		}
		bool flag = chestLog.MoveConsumablesToOwnCategory();
		bool flag2 = !chestLog.DropCountsNormalized;
		if (flag2)
		{
			chestLog.RebuildDropTotalsFromOpeningHistory();
			chestLog.DropCountsNormalized = true;
		}
		bool flag3 = chestLog.ConsolidateTradeRecords();
		if (flag | flag2 | flag3)
		{
			chestLog.Save();
			if (chestLog.LastSaveError == null)
			{
				List<string> list8 = new List<string>();
				if (flag)
				{
					list8.Add("已将历史 DTR Coin 记录归入“消耗品”。");
				}
				if (flag2)
				{
					list8.Add("品质掉落数已按补给箱开启数校正，无法确认的奖励计入“未知”。");
				}
				if (flag3)
				{
					list8.Add("重复的可交易物品记录已合并。");
				}
				text = string.Join(" ", new string[2]
				{
					text,
					string.Join(" ", list8)
				}.Where((string s) => s.Length > 0));
			}
			else
			{
				text = string.Join(" ", new string[2]
				{
					text,
					"统计分类已在内存中修正，但保存失败：" + chestLog.LastSaveError
				}.Where((string s) => s.Length > 0));
			}
		}
		chestLog.LoadWarning = ((text.Length == 0) ? null : text);
		return chestLog;
	}

	private static ChestLog Read(string path)
	{
		return JsonSerializer.Deserialize<ChestLog>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("统计文件内容为空。");
	}

	private static void PreserveCorruptFile()
	{
		try
		{
			if (File.Exists(Path))
			{
				string destFileName = Path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
				File.Copy(Path, destFileName, overwrite: false);
			}
		}
		catch
		{
		}
	}

	private bool MoveConsumablesToOwnCategory()
	{
		bool result = false;
		foreach (ChestRecord record in Records)
		{
			if (GameVision.IsConsumable(record.Name) && !string.Equals(record.Quality, Theme.Consumable.Name, StringComparison.Ordinal))
			{
				record.Quality = Theme.Consumable.Name;
				result = true;
			}
		}
		foreach (ChestRecord tradeRecord in TradeRecords)
		{
			if (GameVision.IsConsumable(tradeRecord.Name) && !string.Equals(tradeRecord.Quality, Theme.Consumable.Name, StringComparison.Ordinal))
			{
				tradeRecord.Quality = Theme.Consumable.Name;
				result = true;
			}
		}
		return result;
	}

	private void RebuildDropTotalsFromOpeningHistory()
	{
		Dictionary<long, int> dictionary = RecentOpenedAt.GroupBy(TimeKey).ToDictionary((IGrouping<long, DateTime> group) => group.Key, (IGrouping<long, DateTime> group) => group.Count());
		Dictionary<string, long> dictionary2 = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		foreach (ChestRecord record in Records)
		{
			record.DropContribution = 0;
		}
		long num = 0L;
		foreach (IGrouping<long, ChestRecord> item in from record in Records
			group record by TimeKey(record.Time))
		{
			if (!dictionary.TryGetValue(item.Key, out var value))
			{
				continue;
			}
			List<ChestRecord> list = item.Where((ChestRecord record) => !string.IsNullOrWhiteSpace(record.Quality) && record.Quality != Theme.Unknown.Name && record.Count > 0).ToList();
			if (list.Count > value)
			{
				continue;
			}
			foreach (ChestRecord item2 in list)
			{
				item2.DropContribution = 1;
				dictionary2[item2.Quality] = dictionary2.GetValueOrDefault(item2.Quality) + 1;
				num++;
			}
		}
		long num2 = Math.Max(0L, TotalOpened - num);
		if (num2 > 0)
		{
			dictionary2[Theme.Unknown.Name] = num2;
		}
		QualityTotals = dictionary2;
	}

	private static long TimeKey(DateTime time)
	{
		return time.ToUniversalTime().Ticks;
	}

	private static string TradeName(string name)
	{
		if (GameVision.IsConsumable(name))
		{
			if (!GameVision.IsMarketable(name))
			{
				return "DTR Coin";
			}
			return "DTR Coin [M]";
		}
		return GameVision.CleanName(name);
	}

	private static string TradeKey(string name, string quality)
	{
		return GameVision.Key(TradeName(name)) + "|" + quality.Trim().ToUpperInvariant();
	}

	private bool ConsolidateTradeRecords()
	{
		if (TradeRecords.Count == 0)
		{
			return false;
		}
		List<ChestRecord> list = (from record in TradeRecords.GroupBy((ChestRecord record) => TradeKey(record.Name, record.Quality), StringComparer.OrdinalIgnoreCase).Select((IGrouping<string, ChestRecord> @group) =>
			{
				ChestRecord chestRecord = @group.MaxBy((ChestRecord record) => record.Time);
				return new ChestRecord
				{
					Name = TradeName(chestRecord.Name),
					Quality = (GameVision.IsConsumable(chestRecord.Name) ? Theme.Consumable.Name : chestRecord.Quality),
					Count = @group.Sum((ChestRecord record) => Math.Max(0, record.Count)),
					Time = chestRecord.Time,
					DropContribution = @group.Sum((ChestRecord record) => record.DropContribution),
					Source = "market-tradable"
				};
			})
			orderby record.Time descending
			select record).ToList();
		int num;
		if (list.Count == TradeRecords.Count)
		{
			num = (list.Where((ChestRecord record, int index) => index >= TradeRecords.Count || record.Name != TradeRecords[index].Name || record.Quality != TradeRecords[index].Quality || record.Count != TradeRecords[index].Count || record.Time != TradeRecords[index].Time || record.DropContribution != TradeRecords[index].DropContribution).Any() ? 1 : 0);
			if (num == 0)
			{
				goto IL_00c3;
			}
		}
		else
		{
			num = 1;
		}
		TradeRecords = list;
		goto IL_00c3;
		IL_00c3:
		return (byte)num != 0;
	}

	public void Save()
	{
		string text = Path + ".tmp";
		try
		{
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
			using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				JsonSerializer.Serialize(fileStream, this, Options);
				fileStream.Flush(flushToDisk: true);
			}
			if (File.Exists(Path))
			{
				File.Copy(Path, BackupPath, overwrite: true);
			}
			File.Move(text, Path, overwrite: true);
			LastSaveError = null;
		}
		catch (Exception ex)
		{
			LastSaveError = ex.Message;
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
		}
	}

	public List<ChestRecord> RecordChestOpen(IEnumerable<ItemEntry> items, int openedCount = 1, string eventId = "", bool persist = true)
	{
		if (openedCount <= 0)
		{
			return new List<ChestRecord>();
		}
		if (!string.IsNullOrWhiteSpace(eventId) && _seenGameEvents.Contains(eventId))
		{
			return new List<ChestRecord>();
		}
		DateTime now = DateTime.Now;
		openedCount = Math.Clamp(openedCount, 1, 10);
		DropCountsNormalized = true;
		TotalOpened += openedCount;
		for (int i = 0; i < openedCount; i++)
		{
			RecentOpenedAt.Add(now);
		}
		RecentOpenedAt = RecentOpenedAt.Where((DateTime time) => time >= now.AddHours(-24.0)).ToList();
		List<ChestRecord> list = new List<ChestRecord>();
		List<ItemEntry> list2 = items.Where((ItemEntry itemEntry) => itemEntry.NameReliable && !string.IsNullOrWhiteSpace(itemEntry.Name)).ToList();
		List<ItemEntry> list3 = ((list2.Count <= openedCount) ? list2 : list2.OrderByDescending((ItemEntry itemEntry) => GameVision.IsMarketable(itemEntry.Name)).Take(openedCount).ToList());
		foreach (ItemEntry item2 in list3)
		{
			string text = (GameVision.IsConsumable(item2.Name) ? Theme.Consumable.Name : item2.Quality);
			ChestRecord chestRecord = new ChestRecord
			{
				Name = item2.Name,
				Quality = text,
				Count = item2.Count,
				Time = now,
				DropContribution = 1,
				Source = "chest-action"
			};
			list.Add(chestRecord);
			QualityTotals[text] = QualityTotals.GetValueOrDefault(text) + 1;
			if (item2.NameReliable && GameVision.IsMarketable(item2.Name))
			{
				AddTradeRecord(chestRecord);
			}
		}
		foreach (ItemEntry item3 in list2.Except(list3))
		{
			list.Add(new ChestRecord
			{
				Name = item3.Name,
				Quality = Theme.Unknown.Name,
				Count = item3.Count,
				Time = now,
				DropContribution = 0,
				Source = "unattributed-reward"
			});
		}
		int num = openedCount - list3.Count;
		if (num > 0)
		{
			QualityTotals[Theme.Unknown.Name] = QualityTotals.GetValueOrDefault(Theme.Unknown.Name) + num;
			ChestRecord item = new ChestRecord
			{
				Name = "未知物品",
				Quality = Theme.Unknown.Name,
				Count = num,
				Time = now,
				DropContribution = num,
				Source = (string.IsNullOrWhiteSpace(eventId) ? "chest-action-unidentified" : "game-api-event"),
				EventId = eventId
			};
			list.Add(item);
		}
		if (!string.IsNullOrWhiteSpace(eventId))
		{
			_seenGameEvents.Add(eventId);
			if (list.Count > 0)
			{
				list[0].EventId = eventId;
				list[0].Source = "game-api-event";
			}
		}
		Records.InsertRange(0, list);
		if (Records.Count > 400)
		{
			Records.RemoveRange(400, Records.Count - 400);
		}
		if (TradeRecords.Count > 400)
		{
			TradeRecords.RemoveRange(400, TradeRecords.Count - 400);
		}
		if (persist)
		{
			Save();
		}
		return list;
	}

	private void AddTradeRecord(ChestRecord record)
	{
		string name = TradeName(record.Name);
		ChestRecord chestRecord = TradeRecords.FirstOrDefault((ChestRecord item) => string.Equals(TradeKey(item.Name, item.Quality), TradeKey(name, record.Quality), StringComparison.OrdinalIgnoreCase));
		if (chestRecord == null)
		{
			TradeRecords.Insert(0, new ChestRecord
			{
				Name = name,
				Quality = record.Quality,
				Count = record.Count,
				Time = record.Time,
				DropContribution = record.DropContribution,
				Source = "market-tradable"
			});
		}
		else
		{
			chestRecord.Name = name;
			chestRecord.Quality = record.Quality;
			chestRecord.Count += record.Count;
			chestRecord.Time = record.Time;
			chestRecord.DropContribution += record.DropContribution;
		}
	}

	public void Clear()
	{
		Records.Clear();
		TradeRecords.Clear();
		KnownItems.Clear();
		RecentOpenedAt.Clear();
		TotalOpened = 0L;
		QualityTotals.Clear();
		DropCountsNormalized = true;
		_seen.Clear();
		_seenGameEvents.Clear();
		Save();
	}
}
