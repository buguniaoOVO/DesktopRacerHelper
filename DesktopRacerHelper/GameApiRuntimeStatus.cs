namespace DesktopRacerHelper;

internal sealed record GameApiRuntimeStatus(string PluginVersion, int Protocol, int HostPid, int Port, string EngineVersion, string GameVersion, bool KernelReady, bool Authenticated, string ConnectionState, bool Connected, long HeartbeatUnixMs);
