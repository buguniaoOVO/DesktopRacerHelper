namespace DesktopRacerHelper;

internal sealed class QualityRule
{
	public string Quality { get; set; } = "";

	public bool Favorite { get; set; }

	public bool Dismantle { get; set; }

	public bool Sell { get; set; }

	public QualityRule()
	{
	}

	public QualityRule(string quality)
	{
		Quality = quality;
	}
}
