using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopRacerHelper;

internal static class Diagnostics
{
	public static async Task BackgroundPanelCheckAsync(string destination)
	{
		GameApiClient api = new GameApiClient();
		GameApiRuntimeStatus status = await api.PingAsync();
		WarehouseScan inventory = PluginAutomationEngine.ParseInventory(await api.GetSnapshotAsync());
		File.WriteAllText(destination, JsonSerializer.Serialize(new
		{
			Success = status.KernelReady,
			HostPid = status.HostPid,
			Port = status.Port,
			PluginVersion = status.PluginVersion,
			Protocol = status.Protocol,
			ConnectionState = status.ConnectionState,
			Inventory = inventory
		}, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}
}
