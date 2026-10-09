using System;

namespace DesktopRacerHelper;

internal sealed class EnvironmentRecord
{
	public string GamePath { get; set; } = "";

	public string ExeSha256 { get; set; } = "";

	public long ExeSize { get; set; }

	public string Engine { get; set; } = "";

	public DateTime DeployedAt { get; set; } = DateTime.Now;

	public int BridgePort { get; set; }

	public string BridgeMode { get; set; } = "";

	public string PluginVersion { get; set; } = "";

	public int PluginProtocol { get; set; }

	public string PluginSha256 { get; set; } = "";
}
