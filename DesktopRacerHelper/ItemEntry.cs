namespace DesktopRacerHelper;

internal sealed record ItemEntry(string Name, string Quality, int GridRow, int GridColumn, int Count = 1, float ClickX = 0f, float ClickY = 0f, bool Selected = false, bool NameReliable = true)
{
	public byte[]? IconPng { get; init; }

	public string Id { get; init; } = "";

	public string SlotId { get; init; } = "";

	public string RarityId { get; init; } = "";

	public string ArchetypeId { get; init; } = "";

	public bool IsConsumable { get; init; }

	public bool IsMarketable { get; init; }

	public bool IsEquipped { get; init; }

	public bool IsLocked { get; init; }
}
