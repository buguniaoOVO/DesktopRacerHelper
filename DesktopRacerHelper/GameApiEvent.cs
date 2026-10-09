using System.Text.Json;

namespace DesktopRacerHelper;

internal sealed record GameApiEvent(long Sequence, string EventId, long OpenedAtUnixMs, JsonElement Item);
