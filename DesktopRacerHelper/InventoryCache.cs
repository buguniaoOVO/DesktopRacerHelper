using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopRacerHelper;

internal static class InventoryCache
{
	private static string CachePath => Path.Combine(Path.GetDirectoryName(AppSettings.SettingsPath), "inventory-cache.json");

	public static WarehouseScan? Load()
	{
		try
		{
			if (!File.Exists(CachePath))
			{
				return null;
			}
			WarehouseScan warehouseScan = JsonSerializer.Deserialize<WarehouseScan>(File.ReadAllText(CachePath));
			if ((object)warehouseScan == null)
			{
				return null;
			}
			return warehouseScan with
			{
				Items = warehouseScan.Items.Select((ItemEntry item) =>
				{
					string name = GameVision.CleanName(item.Name);
					return item with
					{
						Name = name,
						Quality = (GameVision.IsConsumable(name) ? Theme.Consumable.Name : item.Quality)
					};
				}).ToList()
			};
		}
		catch
		{
			return null;
		}
	}

	public static void Save(WarehouseScan scan)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
		File.WriteAllText(CachePath + ".tmp", JsonSerializer.Serialize(scan));
		File.Move(CachePath + ".tmp", CachePath, overwrite: true);
	}
}
