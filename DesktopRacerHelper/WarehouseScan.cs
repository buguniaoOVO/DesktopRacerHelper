using System.Collections.Generic;

namespace DesktopRacerHelper;

internal sealed record WarehouseScan(int SlotUsed, int SlotTotal, List<ItemEntry> Items, string Note, bool Complete = false, bool CanScroll = false);
