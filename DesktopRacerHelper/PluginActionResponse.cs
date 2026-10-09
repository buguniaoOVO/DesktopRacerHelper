using System.Text.Json;

namespace DesktopRacerHelper;

internal sealed record PluginActionResponse(bool Ok, string Error, JsonElement Completed, JsonElement ItemObtained, JsonElement Snapshot);
