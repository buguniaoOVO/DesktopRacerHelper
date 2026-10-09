namespace DesktopRacerHelper;

internal sealed class GameFingerprint
{
	public string ExecutablePath { get; set; } = "";

	public string Sha256 { get; set; } = "";

	public long Size { get; set; }

	public string Engine { get; set; } = "";

	public string Suffix { get; set; } = "";
}
