using System;

namespace DesktopRacerHelper;

internal sealed class ChestRecord
{
	public string Name { get; set; } = "";

	public string Quality { get; set; } = "";

	public DateTime Time { get; set; } = DateTime.Now;

	public int Count { get; set; } = 1;

	public int DropContribution { get; set; } = 1;

	public string Source { get; set; } = "历史记录（未核实）";

	public string EventId { get; set; } = "";
}
