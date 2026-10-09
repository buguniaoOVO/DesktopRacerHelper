using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopRacerHelper;

internal sealed class PluginAutomationEngine
{
	internal sealed record CarCondition(string CarId, double Percent, double ExpectedWearPercent, bool RepairScheduled, double RepairCost = 0.0, double Credits = 0.0)
	{
		public bool CanRepair
		{
			get
			{
				if (!RepairScheduled && RepairCost > 0.0)
				{
					return Credits >= RepairCost;
				}
				return false;
			}
		}
	}

	private sealed record GameItem(string Id, string Name, string Quality, string RarityId, string SlotId, string ArchetypeId, bool IsConsumable, bool IsMarketable, bool IsEquipped, bool IsLocked, int Count, byte[]? IconPng)
	{
		public double StatsStrength { get; init; }
	}

	private static readonly (string InventorySlot, string CollectionSlot)[] SlotOrder = new (string, string)[6]
	{
		("motor", "engine"),
		("transmission", "transmission"),
		("tires", "tires"),
		("suspension", "suspension"),
		("aero", "aerodynamics"),
		("electronics", "electronics")
	};

	private readonly GameApiClient _api;

	private readonly Action<string> _log;

	private readonly Func<AppSettings> _settings;

	private readonly Action<int> _progress;

	private readonly Action<WarehouseScan> _inventory;

	private readonly SemaphoreSlim _ownership;

	private volatile bool _isRunning;

	public bool IsRunning => _isRunning;

	public PluginAutomationEngine(GameApiClient api, Action<string> log, Func<AppSettings> settings, Action<int> progress, Action<WarehouseScan> inventory, SemaphoreSlim ownership)
	{
		_api = api;
		_log = log;
		_settings = settings;
		_progress = progress;
		_inventory = inventory;
		_ownership = ownership;
	}

	public async Task RunAsync(Native.WindowInfo unusedWindow, bool chestDue, bool warehouseDue, bool forceSort, CancellationToken token)
	{
		await _ownership.WaitAsync(token);
		_isRunning = true;
		try
		{
			JsonElement snapshot = await ReadSnapshotAsync(token);
			WarehouseScan obj = ParseInventory(snapshot);
			_inventory(obj);
			_progress(3);
			if (chestDue)
			{
				(JsonElement, int) tuple = await CollectSupplyBoxesAsync(snapshot, token, "定时检测");
				(snapshot, _) = tuple;
				if (tuple.Item2 > 0 && _settings().SortAfterSupplyBoxClaim)
				{
					_log("逐个领取已收到游戏确认；按设置立即执行一轮收藏、拆解和出售。");
					snapshot = await SortAndDisposeAsync(snapshot, token);
				}
			}
			obj = ParseInventory(snapshot);
			_inventory(obj);
			bool flag = obj.SlotTotal > 0 && (double)obj.SlotUsed * 100.0 / (double)obj.SlotTotal >= (double)Math.Clamp(_settings().WarehouseThresholdPercent, 1, 100);
			if (forceSort || (warehouseDue & flag))
			{
				snapshot = await SortAndDisposeAsync(snapshot, token);
				obj = ParseInventory(snapshot);
				_inventory(obj);
				if (_settings().CollectSupplyBoxesAfterSort && GetCrateCount(snapshot) >= 5)
				{
					_log($"整理后游戏数据报告补给箱 {GetCrateCount(snapshot)} 个；开始逐个领取，再执行一轮整理。");
					(JsonElement, int) tuple3 = await CollectSupplyBoxesAsync(snapshot, token, "整理后阈值");
					(snapshot, _) = tuple3;
					if (tuple3.Item2 > 0)
					{
						snapshot = await SortAndDisposeAsync(snapshot, token);
					}
				}
			}
			else if (warehouseDue)
			{
				_log($"仓库占用 {obj.SlotUsed}/{obj.SlotTotal}，未达到 {_settings().WarehouseThresholdPercent}% 整理阈值。");
			}
			obj = ParseInventory(snapshot);
			_inventory(obj);
			_progress(100);
		}
		finally
		{
			_isRunning = false;
			_ownership.Release();
		}
	}

	public async Task<WarehouseScan> RefreshAsync(Native.WindowInfo unusedWindow, CancellationToken token)
	{
		await _ownership.WaitAsync(token);
		try
		{
			WarehouseScan warehouseScan = ParseInventory(await ReadSnapshotAsync(token));
			_inventory(warehouseScan);
			return warehouseScan;
		}
		finally
		{
			_ownership.Release();
		}
	}

	public async Task<CarCondition> ReadCarConditionAsync(Native.WindowInfo unusedWindow, CancellationToken token)
	{
		await _ownership.WaitAsync(token);
		try
		{
			return ParseCarCondition(await ReadSnapshotAsync(token));
		}
		finally
		{
			_ownership.Release();
		}
	}

	public async Task<CarCondition> CheckAndRepairAsync(Native.WindowInfo unusedWindow, int thresholdPercent, CancellationToken token)
	{
		await _ownership.WaitAsync(token);
		_isRunning = true;
		try
		{
			CarCondition carCondition = ParseCarCondition(await ReadSnapshotAsync(token));
			int num = Math.Clamp(thresholdPercent, 1, 100);
			if (carCondition.CarId.Length == 0)
			{
				_log("车况检查：游戏数据未提供车库车辆信息，跳过本轮维修安排。");
				return carCondition;
			}
			_log($"车况检查：{carCondition.CarId} 当前车况 {carCondition.Percent:0.0}%（维修阈值 {num}%）" + (carCondition.RepairScheduled ? "，已安排维修。" : "。"));
			if (carCondition.Percent >= (double)num)
			{
				_log("车况良好，无需安排维修。");
				return carCondition;
			}
			if (carCondition.RepairScheduled)
			{
				_log("车况低于阈值，但游戏已存在待执行维修；不重复提交。");
				return carCondition;
			}
			if (!carCondition.CanRepair)
			{
				_log($"车况低于阈值，但当前无法安排维修（维修费用 {carCondition.RepairCost:0.#}，余额 {carCondition.Credits:0.#}）；本轮跳过。");
				return carCondition;
			}
			_log($"车况检测到 {carCondition.Percent:0.0}% 低于设定阈值 {num}%，点击盾牌图标安排维修。");
			PluginActionResponse pluginActionResponse = await SendActionAsync("repair_car", new
			{
				car_id = carCondition.CarId
			}, token);
			if (!pluginActionResponse.Ok)
			{
				throw new InvalidOperationException("游戏拒绝维修安排：" + DescribeFailure(pluginActionResponse));
			}
			_progress(100);
			CarCondition carCondition2 = ParseCarCondition(pluginActionResponse.Snapshot);
			_log(carCondition2.RepairScheduled ? "维修已安排，将在下一场比赛开始前自动执行。" : "游戏已受理维修请求，等待游戏状态更新。");
			return carCondition2;
		}
		finally
		{
			_isRunning = false;
			_ownership.Release();
		}
	}

	internal static CarCondition ParseCarCondition(JsonElement snapshot)
	{
		JsonElement value = Child(snapshot, "garage");
		string text = GameApiClient.GetString(value, "car_id", GameApiClient.GetString(value, "selected_car_id"));
		double credits = GameApiClient.GetDouble(value, "credits");
		JsonElement jsonElement = Child(value, "cars");
		if (jsonElement.ValueKind == JsonValueKind.Array)
		{
			CarCondition carCondition = null;
			foreach (JsonElement item in jsonElement.EnumerateArray())
			{
				string text2 = GameApiClient.GetString(item, "car_id", GameApiClient.GetString(item, "id"));
				if (GameApiClient.GetBool(item, "owned"))
				{
					CarCondition carCondition2 = new CarCondition(text2, GameApiClient.GetDouble(item, "condition_percent", 100.0), GameApiClient.GetDouble(item, "expected_wear_percent"), GameApiClient.GetBool(item, "repair_scheduled"), GameApiClient.GetDouble(item, "repair_credit_cost"), credits);
					if (text2 == text)
					{
						return carCondition2;
					}
					if ((object)carCondition == null)
					{
						carCondition = carCondition2;
					}
				}
			}
			if ((object)carCondition != null)
			{
				return carCondition;
			}
		}
		return new CarCondition(text, GameApiClient.GetDouble(value, "condition_percent", 100.0), GameApiClient.GetDouble(value, "expected_wear_percent"), GameApiClient.GetBool(value, "repair_scheduled"), GameApiClient.GetDouble(value, "repair_credit_cost"), credits);
	}

	public async Task<(WarehouseScan Scan, int SupplyBoxCount, bool ChestAvailable)> RefreshManualStatsSnapshotAsync(Native.WindowInfo unusedWindow, CancellationToken token)
	{
		JsonElement snapshot = await ReadSnapshotAsync(token);
		int crateCount = GetCrateCount(snapshot);
		return (Scan: ParseInventory(snapshot), SupplyBoxCount: crateCount, ChestAvailable: crateCount > 0);
	}

	private async Task<JsonElement> ReadSnapshotAsync(CancellationToken token)
	{
		JsonElement jsonElement = await _api.GetSnapshotAsync(token);
		string text = GameApiClient.GetString(Child(jsonElement, "connection"), "state");
		if (text != "online")
		{
			throw new InvalidOperationException("游戏内核尚未连接到服务器；当前状态：" + ((text.Length == 0) ? "未知" : text));
		}
		return jsonElement;
	}

	private async Task<JsonElement> SortAndDisposeAsync(JsonElement snapshot, CancellationToken token)
	{
		List<QualityRule> list = _settings().QualityRules.Where((QualityRule rule) => rule.Favorite).ToList();
		if (list.Count == 0)
		{
			_log("收藏策略未勾选品质，跳过收藏。");
		}
		else
		{
			_log("收藏策略：" + string.Join("、", list.Select((QualityRule rule) => rule.Quality)));
			snapshot = await ContributeCollectionPartsAsync(snapshot, list, token);
		}
		snapshot = await DisposeInventoryAsync(snapshot, token, "dismantle_item");
		snapshot = await DisposeInventoryAsync(snapshot, token, "sell_item");
		_inventory(ParseInventory(snapshot));
		_log("本轮游戏数据操作已完成；流程没有打开游戏功能窗口。");
		return snapshot;
	}

	private async Task<JsonElement> ContributeCollectionPartsAsync(JsonElement snapshot, IReadOnlyList<QualityRule> favoriteRules, CancellationToken token)
	{
		Dictionary<string, QualityRule> ruleByRarity = new Dictionary<string, QualityRule>(StringComparer.OrdinalIgnoreCase);
		foreach (QualityRule favoriteRule in favoriteRules)
		{
			string text = RarityId(favoriteRule.Quality);
			if (text.Length > 0)
			{
				ruleByRarity[text] = favoriteRule;
			}
		}
		(string InventorySlot, string CollectionSlot)[] slotOrder = SlotOrder;
		for (int i = 0; i < slotOrder.Length; i++)
		{
			var (inventorySlot, collectionSlot) = slotOrder[i];
			foreach (var (rarityId, rule) in ruleByRarity)
			{
				string setId = collectionSlot + ":" + rarityId;
				HashSet<string> attempted = new HashSet<string>(StringComparer.Ordinal);
				for (int contribution = 0; contribution < 6; contribution++)
				{
					token.ThrowIfCancellationRequested();
					snapshot = await ReadSnapshotAsync(token);
					JsonElement set = FindSet(Child(snapshot, "collection"), setId);
					if (set.ValueKind != JsonValueKind.Object)
					{
						_log($"收藏跳过：{rule.Quality} {collectionSlot} 套装数据尚未同步。");
						break;
					}
					HashSet<string> filled = ReadFilledArchetypes(set);
					GameItem candidate = (from item in ReadItems(Child(snapshot, "inventory"))
						where item.SlotId == inventorySlot && item.RarityId == rarityId && item.Id.Length > 0 && item.ArchetypeId.Length > 0 && !item.IsConsumable && !item.IsMarketable && !item.IsEquipped && !item.IsLocked && !filled.Contains(item.ArchetypeId) && !attempted.Contains(item.Id)
						select item).OrderByDescending(ItemStrength).ThenBy((GameItem item) => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
					if ((object)candidate == null)
					{
						break;
					}
					_log($"收藏投入：{candidate.Name}（{rule.Quality}，{collectionSlot} / {candidate.ArchetypeId}），等待游戏结果。");
					PluginActionResponse response = await SendActionAsync("contribute_collection_part", new
					{
						item_id = candidate.Id,
						set_id = setId,
						archetype_id = candidate.ArchetypeId
					}, token);
					if (!response.Ok)
					{
						snapshot = await ReadSnapshotAsync(token);
						if (!ReadFilledArchetypes(FindSet(Child(snapshot, "collection"), setId)).Contains(candidate.ArchetypeId))
						{
							throw new InvalidOperationException("游戏拒绝收藏投入：" + DescribeFailure(response));
						}
						_log("收藏结果已由游戏状态确认：" + candidate.ArchetypeId + " 槽位已填充。");
						attempted.Add(candidate.Id);
					}
					else
					{
						snapshot = response.Snapshot;
						attempted.Add(candidate.Id);
						_log("收藏已由游戏服务器确认：" + candidate.Name);
						_progress(Math.Min(55, 8 + contribution * 2));
					}
				}
			}
		}
		return await ReadSnapshotAsync(token);
	}

	private async Task<JsonElement> DisposeInventoryAsync(JsonElement snapshot, CancellationToken token, string action)
	{
		List<GameItem> list = new List<GameItem>();
		foreach (GameItem item in ReadItems(Child(snapshot, "inventory")))
		{
			token.ThrowIfCancellationRequested();
			if (item.Id.Length != 0 && !item.IsConsumable && !item.IsMarketable && !item.IsEquipped && !item.IsLocked && item.ArchetypeId.Length != 0)
			{
				QualityRule qualityRule = _settings().RuleOf(item.Quality);
				if ((action == "dismantle_item") ? qualityRule.Dismantle : qualityRule.Sell)
				{
					list.Add(item);
				}
			}
		}
		if (list.Count == 0)
		{
			_log((action == "dismantle_item") ? "批量拆解：没有符合品质策略的未保护零件。" : "批量出售：没有符合品质策略的未保护零件。");
			return snapshot;
		}
		string label = ((action == "dismantle_item") ? "拆解" : "出售");
		_log($"向游戏提交批量{label}：{list.Count} 件；名称带 [M]、已装备、已锁定和消耗品已排除。");
		foreach (string[] batch in list.Select((GameItem item) => item.Id).Distinct(StringComparer.Ordinal).Chunk(100))
		{
			token.ThrowIfCancellationRequested();
			PluginActionResponse pluginActionResponse = await SendActionAsync(action, new
			{
				item_ids = batch
			}, token);
			if (!pluginActionResponse.Ok)
			{
				throw new InvalidOperationException("游戏拒绝批量" + label + "：" + DescribeFailure(pluginActionResponse));
			}
			snapshot = pluginActionResponse.Snapshot;
			_log($"游戏已确认批量{label} {batch.Length} 件。");
		}
		return await ReadSnapshotAsync(token);
	}

	private async Task<(JsonElement Snapshot, int Count)> CollectSupplyBoxesAsync(JsonElement snapshot, CancellationToken token, string source)
	{
		int opened = 0;
		while (true)
		{
			token.ThrowIfCancellationRequested();
			snapshot = await ReadSnapshotAsync(token);
			int crateCount = GetCrateCount(snapshot);
			WarehouseScan warehouseScan = ParseInventory(snapshot);
			if (crateCount <= 0)
			{
				_log(source + "：游戏状态显示没有可领取的补给箱。");
				break;
			}
			if (warehouseScan.SlotTotal > 0 && warehouseScan.SlotUsed >= warehouseScan.SlotTotal)
			{
				_log($"{source}：物品栏已满，停止领取；补给箱剩余 {crateCount} 个。");
				break;
			}
			_log($"{source}：逐个领取补给箱（{crateCount} 个可领），等待这一箱的服务器结果。");
			PluginActionResponse pluginActionResponse = await SendActionAsync("open_next_crate", new Dictionary<string, object>(), token);
			if (!pluginActionResponse.Ok)
			{
				if (pluginActionResponse.Error.Contains("inventory_full", StringComparison.OrdinalIgnoreCase))
				{
					_log("游戏确认物品栏已满，结束本轮补给箱领取。");
					break;
				}
				throw new InvalidOperationException("游戏未确认本次补给箱领取：" + DescribeFailure(pluginActionResponse));
			}
			opened++;
			snapshot = pluginActionResponse.Snapshot;
			_inventory(ParseInventory(snapshot));
			_progress(Math.Min(90, 10 + opened * 2));
		}
		return (Snapshot: snapshot, Count: opened);
	}

	private async Task<PluginActionResponse> SendActionAsync(string action, object payload, CancellationToken token)
	{
		PluginActionResponse response = null;
		await ActionGate.Shared.ExecuteAsync(async () =>
		{
			response = await _api.SendActionAsync(action, payload, token);
		}, token);
		return response ?? throw new InvalidOperationException("没有收到游戏内 API 的动作结果。");
	}

	private static string DescribeFailure(PluginActionResponse response)
	{
		if (response.Error.Length <= 0)
		{
			return GameApiClient.GetString(response.Completed, "diagnostic_message", "游戏未返回失败原因。");
		}
		return response.Error;
	}

	internal static WarehouseScan ParseInventory(JsonElement snapshot)
	{
		JsonElement jsonElement = Child(snapshot, "inventory");
		List<ItemEntry> list = ReadItems(jsonElement).Select((GameItem item, int index) => new ItemEntry(item.Name, item.Quality, index / 6, index % 6, Math.Max(1, item.Count), 0f, 0f, Selected: false, item.Name.Length > 0)
		{
			Id = item.Id,
			SlotId = item.SlotId,
			RarityId = item.RarityId,
			ArchetypeId = item.ArchetypeId,
			IsConsumable = item.IsConsumable,
			IsMarketable = item.IsMarketable,
			IsEquipped = item.IsEquipped,
			IsLocked = item.IsLocked,
			IconPng = item.IconPng
		}).ToList();
		return new WarehouseScan(SlotTotal: GameApiClient.GetInt(jsonElement, "capacity"), SlotUsed: GameApiClient.GetInt(jsonElement, "count", list.Count), Items: list, Note: "source.gameApi", Complete: true);
	}

	private static List<GameItem> ReadItems(JsonElement inventory)
	{
		List<GameItem> list = new List<GameItem>();
		if (!inventory.TryGetProperty("items", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return list;
		}
		foreach (JsonElement item in value.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			string text = GameApiClient.GetString(item, "name").Trim();
			string rarityId = GameApiClient.GetString(item, "rarity_id", "common");
			bool flag = GameVision.IsConsumable(text) || GameApiClient.GetString(item, "definition_id").StartsWith("consumable", StringComparison.OrdinalIgnoreCase);
			bool flag2 = GameApiClient.GetBool(item, "steam_item") || GameApiClient.GetBool(item, "marketable") || GameVision.IsMarketable(text);
			if (flag2 && !GameVision.IsMarketable(text))
			{
				text += " [M]";
			}
			byte[] iconPng = null;
			string text2 = GameApiClient.GetString(item, "helper_icon_png");
			if (text2.Length > 0)
			{
				try
				{
					iconPng = Convert.FromBase64String(text2);
				}
				catch
				{
				}
			}
			list.Add(new GameItem(GameApiClient.GetString(item, "id", GameApiClient.GetString(item, "instance_id")), text, flag ? Theme.Consumable.Name : QualityName(rarityId), rarityId, GameApiClient.GetString(item, "slot_id"), GameApiClient.GetString(item, "archetype_id"), flag, flag2, GameApiClient.GetBool(item, "equipped"), GameApiClient.GetBool(item, "locked"), GameApiClient.GetInt(item, "count", GameApiClient.GetInt(item, "quantity", 1)), iconPng)
			{
				StatsStrength = ReadStatsStrength(item)
			});
		}
		return list;
	}

	private static string QualityName(string rarityId)
	{
		return rarityId switch
		{
			"common" => "普通", 
			"uncommon" => "优秀", 
			"rare" => "稀有", 
			"epic" => "史诗", 
			"legendary" => "传说", 
			"mythic" => "神话", 
			"exotic" => "异域", 
			_ => Theme.Unknown.Name, 
		};
	}

	private static string RarityId(string quality)
	{
		return quality switch
		{
			"普通" => "common", 
			"优秀" => "uncommon", 
			"稀有" => "rare", 
			"史诗" => "epic", 
			"传说" => "legendary", 
			"神话" => "mythic", 
			"异域" => "exotic", 
			_ => "", 
		};
	}

	private static double ItemStrength(GameItem item)
	{
		return item.StatsStrength;
	}

	private static double ReadStatsStrength(JsonElement item)
	{
		JsonElement jsonElement = Child(item, "stats_exact");
		double num = 0.0;
		string[] array = new string[5] { "vel", "arr", "fre", "est", "aer" };
		foreach (string propertyName in array)
		{
			if (jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var value2))
			{
				num += value2;
			}
		}
		return num;
	}

	private static JsonElement Child(JsonElement value, string key)
	{
		if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var value2))
		{
			return default;
		}
		return value2;
	}

	private static int GetCrateCount(JsonElement snapshot)
	{
		return Math.Max(0, GameApiClient.GetInt(Child(snapshot, "hud"), "crate_count"));
	}

	private static JsonElement FindSet(JsonElement collection, string setId)
	{
		JsonElement jsonElement = Child(collection, "sets");
		if (jsonElement.ValueKind != JsonValueKind.Array)
		{
			return default;
		}
		foreach (JsonElement item in jsonElement.EnumerateArray())
		{
			if (string.Equals(GameApiClient.GetString(item, "id"), setId, StringComparison.Ordinal))
			{
				return item;
			}
		}
		return default;
	}

	private static HashSet<string> ReadFilledArchetypes(JsonElement set)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		JsonElement jsonElement = Child(set, "filled");
		if (jsonElement.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in jsonElement.EnumerateArray())
			{
				string text = GameApiClient.GetString(item, "archetype_id");
				if (text.Length > 0)
				{
					hashSet.Add(text);
				}
			}
		}
		return hashSet;
	}
}
