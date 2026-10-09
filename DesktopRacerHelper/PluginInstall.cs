using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopRacerHelper;

internal static class PluginInstall
{
	internal sealed record PluginStatus(string Version, int Protocol, int HostPid, bool ApiReady, bool Connected, string ConnectionState);

	public sealed record EnsureResult(bool Ok, bool Changed, string Message, string ExecutablePath = "", bool SteamLaunchReady = false);

	private sealed record SteamLaunchResult(bool Ready, string Message);

	private sealed class PluginManifest
	{
		public string SourceExecutable { get; set; } = "";

		public string SourceSha256 { get; set; } = "";

		public string PatchedSha256 { get; set; } = "";

		public string PluginBundleHash { get; set; } = "";

		public string PluginVersion { get; set; } = "";

		public int Protocol { get; set; }

		public DateTimeOffset PatchedAt { get; set; }

		public string OriginalBackupPath { get; set; } = "";
	}

	public const int ExpectedProtocol = 3;

	public const string ExpectedVersion = "1.0.2";

	private const string HelperExecutablePrefix = "Desk Top Racer Helper-";

	private const string CachedHelperExecutablePrefix = "DesktopRacerHelper-";

	private const string GameExecutableName = "Desk Top Racer.exe";

	private const string OriginalBackupPrefix = "Desk Top Racer.exe.helper-original-";

	private const string GdreVersion = "v2.7.0";

	private const string GdreArchiveName = "GDRE_tools-v2.7.0-windows.zip";

	private const string GdreSha256 = "92a8d5d3d5c0cca159fc6cc34ebdda2cf7e1b448035cdabe1a67f0981cbd0a2c";

	private const string BridgeAutoloadName = "DesktopRacerHelperBridge";

	private const string BridgeAutoloadPath = "res://scripts/app/helper_bridge.gd";

	private static string BridgeAssetPath => Path.Combine(AppContext.BaseDirectory, "Assets", "game-plugin", "helper_bridge.gd");

	private static string ToolDirectory => Path.Combine(Deployment.Folder, "tools", "gdre-2.7.0");

	private static string ToolPath => Path.Combine(ToolDirectory, "gdre_tools.exe");

	private static string ArchivePath => Path.Combine(Deployment.Folder, "tools", "GDRE_tools-v2.7.0-windows.zip");

	private static string BundledArchivePath => Path.Combine(AppContext.BaseDirectory, "Assets", "tools", GdreArchiveName);

	public static string? BundledHash
	{
		get
		{
			if (!File.Exists(BridgeAssetPath))
			{
				return null;
			}
			return BundleHash();
		}
	}

	public static string GetSourceExecutablePath(string executablePath)
	{
		string name = Path.GetFileName(executablePath);
		if (name.StartsWith(OriginalBackupPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return executablePath;
		}
		PluginManifest? helperManifest = ReadManifest(ManifestPath(executablePath));
		if (helperManifest != null && !string.IsNullOrWhiteSpace(helperManifest.SourceExecutable))
		{
			string manifestSource = CanonicalGameExecutablePath(helperManifest.SourceExecutable);
			return ResolveSteamOriginalBackup(manifestSource) ?? manifestSource;
		}
		string gameExecutable = CanonicalGameExecutablePath(executablePath);
		return ResolveSteamOriginalBackup(gameExecutable) ?? gameExecutable;
	}

	public static string CanonicalGameExecutablePath(string executablePath)
	{
		string directory = Path.GetDirectoryName(executablePath) ?? "";
		string name = Path.GetFileName(executablePath);
		if (name.StartsWith(OriginalBackupPrefix, StringComparison.OrdinalIgnoreCase)
			|| (name.StartsWith(HelperExecutablePrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
		{
			return Path.Combine(directory, GameExecutableName);
		}
		return executablePath;
	}

	public static bool IsSteamLaunchReady(string executablePath)
	{
		string gameExecutable = CanonicalGameExecutablePath(executablePath);
		string manifestPath = SteamManifestPath(gameExecutable);
		try
		{
			PluginManifest? manifest = ReadManifest(manifestPath);
			return manifest != null
				&& File.Exists(gameExecutable)
				&& string.Equals(Deployment.HashFile(gameExecutable), manifest.PatchedSha256, StringComparison.OrdinalIgnoreCase)
				&& File.Exists(manifest.OriginalBackupPath)
				&& string.Equals(Deployment.HashFile(manifest.OriginalBackupPath), manifest.SourceSha256, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	public static string HelperExecutablePath(string sourceExecutable)
	{
		string sourceExecutablePath = GetSourceExecutablePath(sourceExecutable);
		string text = Deployment.HashFile(sourceExecutablePath);
		string text2 = BundleHash();
		string cacheDirectory = Path.Combine(Deployment.Folder, "cache");
		return Path.Combine(cacheDirectory, $"{CachedHelperExecutablePrefix}{ExpectedVersion}-{text.Substring(0, 12)}-{text2.Substring(0, 8)}.exe");
	}

	public static string? DeployedHash(string gameDirectory)
	{
		try
		{
			string text = Path.Combine(gameDirectory, "Desk Top Racer.exe");
			if (!File.Exists(text))
			{
				return null;
			}
			string path = ManifestPath(HelperExecutablePath(text));
			if (!File.Exists(path))
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			string text2 = GetString(jsonDocument.RootElement, "pluginBundleHash");
			return (text2.Length == 0) ? null : text2;
		}
		catch
		{
			return null;
		}
	}

	public static PluginStatus? ReadRuntimeStatus()
	{
		Native.WindowInfo windowInfo = GameReader.FindGameWindow();
		GameApiRuntimeStatus gameApiRuntimeStatus = GameApiClient.ReadRuntimeStatus((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		if ((object)gameApiRuntimeStatus != null)
		{
			return new PluginStatus(gameApiRuntimeStatus.PluginVersion, gameApiRuntimeStatus.Protocol, gameApiRuntimeStatus.HostPid, gameApiRuntimeStatus.KernelReady, gameApiRuntimeStatus.Connected, gameApiRuntimeStatus.ConnectionState);
		}
		return null;
	}

	public static EnsureResult Ensure(string executablePath)
	{
		string sourceExecutablePath = GetSourceExecutablePath(executablePath);
		if (string.IsNullOrWhiteSpace(sourceExecutablePath) || !File.Exists(sourceExecutablePath))
		{
			return new EnsureResult(Ok: false, Changed: false, "游戏原版可执行文件不存在。");
		}
		if (!File.Exists(BridgeAssetPath))
		{
			return new EnsureResult(Ok: false, Changed: false, "随包游戏 API 插件资源缺失。");
		}
		string text;
		string text2;
		try
		{
			text = Deployment.HashFile(sourceExecutablePath);
			text2 = BundleHash();
		}
		catch (Exception ex)
		{
			return new EnsureResult(Ok: false, Changed: false, "读取游戏或接口版本失败：" + ex.Message);
		}
		string text3 = HelperExecutablePath(sourceExecutablePath);
		string text4 = ManifestPath(text3);
		Directory.CreateDirectory(Path.GetDirectoryName(text3) ?? Deployment.Folder);
		TryMigrateLegacyHelperExecutable(sourceExecutablePath, text, text2, text3, text4);
		if (IsCurrentDeployment(text3, text4, text, text2))
		{
			return CompleteEnsure(sourceExecutablePath, text3, text, text2, changed: false);
		}
		(bool, string) tuple = EnsureGdreTool();
		if (!tuple.Item1)
		{
			return new EnsureResult(Ok: false, Changed: false, tuple.Item2);
		}
		string text5 = text3 + ".new";
		try
		{
			if (File.Exists(text5))
			{
				File.Delete(text5);
			}
			(bool, string, string) tuple2 = PrepareProjectBinary(sourceExecutablePath);
			if (!tuple2.Item1)
			{
				throw new InvalidOperationException(tuple2.Item2);
			}
			(bool, string) tuple3 = RunTool(new string[5]
			{
				"--pck-patch=" + sourceExecutablePath,
				"--output=" + text5,
				"--embed=" + sourceExecutablePath,
				"--patch-file=" + tuple2.Item3 + "=res://project.binary",
				"--patch-file=" + BridgeAssetPath + "=res://scripts/app/helper_bridge.gd"
			}, TimeSpan.FromMinutes(5.0));
			if (!tuple3.Item1 || !File.Exists(text5))
			{
				throw new InvalidOperationException("GDRE 补丁失败：" + tuple3.Item2);
			}
			GameFingerprint gameFingerprint = Deployment.Inspect(text5);
			if (gameFingerprint == null || !string.Equals(gameFingerprint.Engine, "Godot", StringComparison.OrdinalIgnoreCase) || !gameFingerprint.Suffix.StartsWith("4.7.1", StringComparison.Ordinal))
			{
				throw new InvalidDataException("补丁产物未保留当前 Godot 游戏版本。");
			}
			string patchedSha = Deployment.HashFile(text5);
			File.Move(text5, text3, overwrite: true);
			SaveManifest(text4, new PluginManifest
			{
				SourceExecutable = CanonicalGameExecutablePath(sourceExecutablePath),
				SourceSha256 = text,
				PatchedSha256 = patchedSha,
				PluginBundleHash = text2,
				PluginVersion = "1.0.2",
				Protocol = 3,
				PatchedAt = DateTimeOffset.UtcNow
			});
			return CompleteEnsure(sourceExecutablePath, text3, text, text2, changed: true);
		}
		catch (Exception ex2)
		{
			try
			{
				if (File.Exists(text5))
				{
					File.Delete(text5);
				}
			}
			catch
			{
			}
			return new EnsureResult(Ok: false, Changed: false, ex2.Message);
		}
	}

	private static bool TryMigrateLegacyHelperExecutable(string sourceExecutable, string sourceHash, string bundleHash, string cacheExecutable, string cacheManifest)
	{
		try
		{
			string sourceDirectory = Path.GetDirectoryName(CanonicalGameExecutablePath(sourceExecutable)) ?? "";
			string legacyExecutable = Path.Combine(sourceDirectory, $"{HelperExecutablePrefix}{ExpectedVersion}-{sourceHash.Substring(0, 12)}-{bundleHash.Substring(0, 8)}.exe");
			string legacyManifest = ManifestPath(legacyExecutable);
			if (!IsCurrentDeployment(legacyExecutable, legacyManifest, sourceHash, bundleHash))
			{
				return false;
			}
			File.Copy(legacyExecutable, cacheExecutable, overwrite: true);
			PluginManifest manifest = ReadManifest(legacyManifest) ?? throw new InvalidDataException("旧版游戏缓存清单读取失败。");
			manifest.SourceExecutable = CanonicalGameExecutablePath(sourceExecutable);
			SaveManifest(cacheManifest, manifest);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static EnsureResult CompleteEnsure(string sourceExecutable, string helperExecutable, string sourceHash, string bundleHash, bool changed)
	{
		SteamLaunchResult steamResult = EnsureSteamLaunch(sourceExecutable, helperExecutable, sourceHash, bundleHash);
		if (steamResult.Ready)
		{
			CleanupLegacyHelperExecutables(CanonicalGameExecutablePath(sourceExecutable));
		}
		return new EnsureResult(Ok: true, Changed: changed, steamResult.Message, helperExecutable, steamResult.Ready);
	}

	private static void CleanupLegacyHelperExecutables(string gameExecutable)
	{
		string directory = Path.GetDirectoryName(gameExecutable) ?? "";
		if (!Directory.Exists(directory))
		{
			return;
		}
		string[] legacyFiles;
		try
		{
			legacyFiles = Directory.GetFiles(directory, HelperExecutablePrefix + "*.exe");
		}
		catch
		{
			return;
		}
		foreach (string legacyExecutable in legacyFiles)
		{
			PluginManifest? manifest = ReadManifest(ManifestPath(legacyExecutable));
			if (manifest == null || !string.Equals(CanonicalGameExecutablePath(manifest.SourceExecutable), gameExecutable, StringComparison.OrdinalIgnoreCase) || IsProcessUsing(legacyExecutable))
			{
				continue;
			}
			try
			{
				File.Delete(legacyExecutable);
				string manifestPath = ManifestPath(legacyExecutable);
				if (File.Exists(manifestPath))
				{
					File.Delete(manifestPath);
				}
			}
			catch
			{
			}
		}
	}

	private static SteamLaunchResult EnsureSteamLaunch(string sourceExecutable, string helperExecutable, string sourceHash, string bundleHash)
	{
		string gameExecutable = CanonicalGameExecutablePath(sourceExecutable);
		string manifestPath = SteamManifestPath(gameExecutable);
		string helperManifest = ManifestPath(helperExecutable);
		string stagedExecutable = gameExecutable + ".steam-patch-" + Guid.NewGuid().ToString("N") + ".tmp";
		string? previousManifest = null;
		try
		{
			if (!File.Exists(gameExecutable) || !IsCurrentDeployment(helperExecutable, helperManifest, sourceHash, bundleHash))
			{
				return new SteamLaunchResult(false, "游戏接口文件校验失败，Steam 启动文件保持原样。");
			}
			string patchedHash = Deployment.HashFile(helperExecutable);
			string currentHash = Deployment.HashFile(gameExecutable);
			PluginManifest? oldManifest = ReadManifest(manifestPath);
			if (oldManifest != null
				&& string.Equals(currentHash, patchedHash, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(oldManifest.SourceSha256, sourceHash, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(oldManifest.PatchedSha256, patchedHash, StringComparison.OrdinalIgnoreCase)
				&& IsValidBackup(oldManifest))
			{
				return new SteamLaunchResult(true, "Steam 游戏接口已就绪。");
			}
			if (IsProcessUsing(gameExecutable))
			{
				return new SteamLaunchResult(false, "游戏环境已准备；退出当前游戏后，助手会完成 Steam 启动接入。");
			}
			string resolvedSource = GetSourceExecutablePath(sourceExecutable);
			if (!File.Exists(resolvedSource) || !string.Equals(Deployment.HashFile(resolvedSource), sourceHash, StringComparison.OrdinalIgnoreCase))
			{
				return new SteamLaunchResult(false, "Steam 游戏文件已变化，请在环境页重新检测和部署。");
			}
			bool currentIsOriginal = string.Equals(currentHash, sourceHash, StringComparison.OrdinalIgnoreCase);
			bool currentIsManagedPatch = oldManifest != null
				&& string.Equals(currentHash, oldManifest.PatchedSha256, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(oldManifest.SourceSha256, sourceHash, StringComparison.OrdinalIgnoreCase)
				&& IsValidBackup(oldManifest);
			if (!currentIsOriginal && !currentIsManagedPatch)
			{
				return new SteamLaunchResult(false, "Steam 启动文件与已保存的原版都不匹配；助手保留了当前游戏文件，请重新检测环境。");
			}

			string backupPath = currentIsManagedPatch
				? oldManifest!.OriginalBackupPath
				: BackupPathFor(gameExecutable, sourceHash);
			if (!currentIsManagedPatch && (!File.Exists(backupPath) || !string.Equals(Deployment.HashFile(backupPath), sourceHash, StringComparison.OrdinalIgnoreCase)))
			{
				CopyFileAtomically(gameExecutable, backupPath);
			}
			if (!File.Exists(backupPath) || !string.Equals(Deployment.HashFile(backupPath), sourceHash, StringComparison.OrdinalIgnoreCase))
			{
				return new SteamLaunchResult(false, "原版 EXE 备份校验失败；Steam 启动文件保持原样。");
			}

			File.Copy(helperExecutable, stagedExecutable, overwrite: true);
			if (!string.Equals(Deployment.HashFile(stagedExecutable), patchedHash, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Steam 启动暂存文件校验失败。");
			}
			previousManifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;
			SaveManifest(manifestPath, new PluginManifest
			{
				SourceExecutable = gameExecutable,
				SourceSha256 = sourceHash,
				PatchedSha256 = patchedHash,
				PluginBundleHash = bundleHash,
				PluginVersion = ExpectedVersion,
				Protocol = ExpectedProtocol,
				PatchedAt = DateTimeOffset.UtcNow,
				OriginalBackupPath = backupPath
			});
			try
			{
				File.Replace(stagedExecutable, gameExecutable, null);
			}
			catch
			{
				RestoreManifest(manifestPath, previousManifest);
				throw;
			}
			if (!string.Equals(Deployment.HashFile(gameExecutable), patchedHash, StringComparison.OrdinalIgnoreCase))
			{
				CopyFileAtomically(backupPath, gameExecutable);
				RestoreManifest(manifestPath, previousManifest);
				return new SteamLaunchResult(false, "Steam 启动文件替换后的校验失败；已恢复原版 EXE。");
			}
			RemoveStaleOriginalBackups(gameExecutable, backupPath);
			return new SteamLaunchResult(true, "Steam 游戏接口已接入，并已保存原始 EXE 备份。");
		}
		catch (Exception ex)
		{
			return new SteamLaunchResult(false, "Steam 启动接入失败：" + ex.Message);
		}
		finally
		{
			try
			{
				if (File.Exists(stagedExecutable))
				{
					File.Delete(stagedExecutable);
				}
			}
			catch
			{
			}
		}
	}

	private static string? ResolveSteamOriginalBackup(string gameExecutable)
	{
		try
		{
			PluginManifest? manifest = ReadManifest(SteamManifestPath(gameExecutable));
			if (manifest == null || !File.Exists(gameExecutable) || !IsValidBackup(manifest))
			{
				return null;
			}
			return string.Equals(Deployment.HashFile(gameExecutable), manifest.PatchedSha256, StringComparison.OrdinalIgnoreCase)
				? manifest.OriginalBackupPath
				: null;
		}
		catch
		{
			return null;
		}
	}

	private static bool IsValidBackup(PluginManifest manifest)
	{
		return !string.IsNullOrWhiteSpace(manifest.OriginalBackupPath)
			&& File.Exists(manifest.OriginalBackupPath)
			&& string.Equals(Deployment.HashFile(manifest.OriginalBackupPath), manifest.SourceSha256, StringComparison.OrdinalIgnoreCase);
	}

	private static string BackupPathFor(string gameExecutable, string sourceHash)
	{
		string directory = Path.GetDirectoryName(gameExecutable) ?? "";
		return Path.Combine(directory, OriginalBackupPrefix + sourceHash.Substring(0, 12));
	}

	private static void RemoveStaleOriginalBackups(string gameExecutable, string? keepPath)
	{
		string directory = Path.GetDirectoryName(gameExecutable) ?? "";
		if (!Directory.Exists(directory))
		{
			return;
		}
		string[] backups;
		try
		{
			backups = Directory.GetFiles(directory, OriginalBackupPrefix + "*");
		}
		catch
		{
			return;
		}
		foreach (string backup in backups)
		{
			if (keepPath != null && string.Equals(Path.GetFullPath(backup), Path.GetFullPath(keepPath), StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			try
			{
				File.Delete(backup);
			}
			catch
			{
			}
		}
	}

	private static string SteamManifestPath(string gameExecutable)
	{
		return gameExecutable + ".helper-plugin.json";
	}

	private static PluginManifest? ReadManifest(string path)
	{
		try
		{
			return File.Exists(path) ? JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path)) : null;
		}
		catch
		{
			return null;
		}
	}

	private static void CopyFileAtomically(string source, string destination)
	{
		string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
		try
		{
			File.Copy(source, temporary, overwrite: true);
			if (File.Exists(destination))
			{
				File.Replace(temporary, destination, null);
			}
			else
			{
				File.Move(temporary, destination);
			}
		}
		finally
		{
			if (File.Exists(temporary))
			{
				File.Delete(temporary);
			}
		}
	}

	private static void RestoreManifest(string path, string? previousManifest)
	{
		if (previousManifest == null)
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		else
		{
			string temporary = path + ".restore-" + Guid.NewGuid().ToString("N");
			File.WriteAllText(temporary, previousManifest);
			File.Move(temporary, path, overwrite: true);
		}
	}

	public static bool RestoreOriginalSteamLibrary(string executablePath, out string message)
	{
		string directoryName = Path.GetDirectoryName(GetSourceExecutablePath(executablePath));
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			message = "无法确定游戏目录。";
			return false;
		}
		string destFileName = Path.Combine(directoryName, "steam_api64.dll");
		string text = Path.Combine(directoryName, "steam_api64.dll.bak-original");
		string text2 = Path.Combine(directoryName, "steam_api64_real.dll");
		try
		{
			if (File.Exists(text))
			{
				File.Copy(text, destFileName, overwrite: true);
			}
			else
			{
				if (!File.Exists(text2))
				{
					message = "未发现旧画面代理插件，无需还原。";
					return true;
				}
				File.Copy(text2, destFileName, overwrite: true);
			}
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
			message = "旧画面代理已还原为游戏原版 Steam 库。";
			return true;
		}
		catch (Exception ex)
		{
			message = "游戏关闭后才能还原旧画面代理：" + ex.Message;
			return false;
		}
	}

	public static EnsureResult Remove(string? executablePath)
	{
		if (string.IsNullOrWhiteSpace(executablePath))
		{
			return new EnsureResult(Ok: false, Changed: false, "游戏路径缺失。");
		}
		string gameExecutable = CanonicalGameExecutablePath(executablePath);
		string sourceExecutablePath = GetSourceExecutablePath(gameExecutable);
		string text = "";
		try
		{
			text = HelperExecutablePath(sourceExecutablePath);
			string directory = Path.GetDirectoryName(gameExecutable) ?? "";
			List<string> helperExecutables = Directory.Exists(directory)
				? Directory.GetFiles(directory, HelperExecutablePrefix + "*.exe").Where(path =>
				{
					PluginManifest? manifest = ReadManifest(ManifestPath(path));
					return manifest != null && string.Equals(CanonicalGameExecutablePath(manifest.SourceExecutable), gameExecutable, StringComparison.OrdinalIgnoreCase);
				}).ToList()
				: new List<string>();
			if (!helperExecutables.Contains(text, StringComparer.OrdinalIgnoreCase))
			{
				helperExecutables.Add(text);
			}
			if (IsProcessUsing(gameExecutable) || helperExecutables.Any(IsProcessUsing))
			{
				return new EnsureResult(Ok: false, Changed: false, "游戏仍在运行；关闭游戏后再清除插件。", text);
			}
			(bool restoreOk, string restoreMessage) = RestoreSteamLaunch(gameExecutable);
			if (!restoreOk)
			{
				return new EnsureResult(Ok: false, Changed: false, restoreMessage, text);
			}
			RestoreOriginalSteamLibrary(sourceExecutablePath, out string message);
			foreach (string helperExecutable in helperExecutables)
			{
				if (File.Exists(helperExecutable))
				{
					File.Delete(helperExecutable);
				}
				string helperManifest = ManifestPath(helperExecutable);
				if (File.Exists(helperManifest))
				{
					File.Delete(helperManifest);
				}
			}
			return new EnsureResult(Ok: true, Changed: true, restoreMessage + " " + message, text);
		}
		catch (Exception ex)
		{
			return new EnsureResult(Ok: false, Changed: false, ex.Message, text);
		}
	}

	private static (bool Ok, string Message) RestoreSteamLaunch(string gameExecutable)
	{
		string manifestPath = SteamManifestPath(gameExecutable);
		PluginManifest? manifest = ReadManifest(manifestPath);
		if (manifest == null)
		{
			return (Ok: true, Message: "Steam 启动文件没有助手补丁。");
		}
		try
		{
			if (IsProcessUsing(gameExecutable))
			{
				return (Ok: false, Message: "Steam 游戏仍在运行；退出后再清除插件。");
			}
			string currentHash = Deployment.HashFile(gameExecutable);
			bool currentIsPatched = string.Equals(currentHash, manifest.PatchedSha256, StringComparison.OrdinalIgnoreCase);
			bool currentIsOriginal = string.Equals(currentHash, manifest.SourceSha256, StringComparison.OrdinalIgnoreCase);
			if (!currentIsPatched && !currentIsOriginal)
			{
				return (Ok: false, Message: "Steam 游戏 EXE 已发生其他变化；备份和接口文件均已保留，请重新检测环境后再清除。");
			}
			if (currentIsPatched)
			{
				if (!IsValidBackup(manifest))
				{
					return (Ok: false, Message: "原版 EXE 备份校验失败；Steam 启动文件保持原样。");
				}
				CopyFileAtomically(manifest.OriginalBackupPath, gameExecutable);
				if (!string.Equals(Deployment.HashFile(gameExecutable), manifest.SourceSha256, StringComparison.OrdinalIgnoreCase))
				{
					return (Ok: false, Message: "恢复后的原版 EXE 校验失败；备份文件已保留。");
				}
			}
			File.Delete(manifestPath);
			RemoveStaleOriginalBackups(gameExecutable, keepPath: null);
			return (Ok: true, Message: currentIsPatched ? "Steam 原版启动文件已恢复。" : "Steam 原版启动文件已保留。");
		}
		catch (Exception ex)
		{
			return (Ok: false, Message: "恢复 Steam 原版启动文件失败：" + ex.Message);
		}
	}

	private static bool IsCurrentDeployment(string target, string manifestPath, string sourceHash, string bundleHash)
	{
		try
		{
			if (!File.Exists(target) || !File.Exists(manifestPath))
			{
				return false;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
			JsonElement rootElement = jsonDocument.RootElement;
			return GetString(rootElement, "sourceSha256") == sourceHash && GetString(rootElement, "pluginBundleHash") == bundleHash && GetString(rootElement, "pluginVersion") == "1.0.2" && GetInt(rootElement, "protocol") == 3 && GetString(rootElement, "patchedSha256") == Deployment.HashFile(target);
		}
		catch
		{
			return false;
		}
	}

	private static (bool Ok, string Message) EnsureGdreTool()
	{
		if (File.Exists(ToolPath) && File.Exists(Path.Combine(ToolDirectory, "gdre_tools.pck")) && File.Exists(Path.Combine(ToolDirectory, "GodotMonoDecompNativeAOT.dll")))
		{
			return (Ok: true, Message: "");
		}
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(ArchivePath));
			if (!File.Exists(ArchivePath))
			{
				if (File.Exists(BundledArchivePath))
				{
					File.Copy(BundledArchivePath, ArchivePath, overwrite: true);
				}
				else
				{
					using HttpClient httpClient = new HttpClient
					{
						Timeout = TimeSpan.FromMinutes(3.0)
					};
					string requestUri = "https://github.com/GDRETools/gdsdecomp/releases/download/v2.7.0/GDRE_tools-v2.7.0-windows.zip";
					using HttpResponseMessage httpResponseMessage = httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
					httpResponseMessage.EnsureSuccessStatusCode();
					using Stream stream = httpResponseMessage.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
					using FileStream destination = new FileStream(ArchivePath, FileMode.Create, FileAccess.Write, FileShare.None);
					stream.CopyTo(destination);
				}
			}
			if (!string.Equals(Deployment.HashFile(ArchivePath), "92a8d5d3d5c0cca159fc6cc34ebdda2cf7e1b448035cdabe1a67f0981cbd0a2c", StringComparison.OrdinalIgnoreCase))
			{
				File.Delete(ArchivePath);
				return (Ok: false, Message: "Godot 项目补丁工具校验失败；未修改游戏文件。");
			}
			Directory.CreateDirectory(ToolDirectory);
			ZipFile.ExtractToDirectory(ArchivePath, ToolDirectory, overwriteFiles: true);
			return File.Exists(ToolPath) ? (Ok: true, Message: "") : (Ok: false, Message: "GDRE 解压包中缺少 gdre_tools.exe；未修改游戏文件。");
		}
		catch (Exception ex)
		{
			return (Ok: false, Message: "无法准备 Godot 插件补丁工具：" + ex.Message);
		}
	}

	private static string BundleHash()
	{
		using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		incrementalHash.AppendData(File.ReadAllBytes(BridgeAssetPath));
		return Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
	}

	private static (bool Ok, string Message, string ConfigPath) PrepareProjectBinary(string source)
	{
		string text = Path.Combine(Deployment.Folder, "tools", "patch-work");
		try
		{
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
			string text2 = Path.Combine(text, "extract");
			string text3 = Path.Combine(text, "text");
			string text4 = Path.Combine(text, "binary");
			Directory.CreateDirectory(text2);
			Directory.CreateDirectory(text3);
			Directory.CreateDirectory(text4);
			(bool, string) tuple = RunTool(new string[3]
			{
				"--extract=" + source,
				"--include=res://project.binary",
				"--output=" + text2
			}, TimeSpan.FromMinutes(3.0));
			string text5 = Path.Combine(text2, "project.binary");
			if (!tuple.Item1 || !File.Exists(text5))
			{
				return (Ok: false, Message: "读取游戏自带工程配置失败：" + tuple.Item2, ConfigPath: "");
			}
			(bool, string) tuple2 = RunTool(new string[2]
			{
				"--bin-to-txt=" + text5,
				"--output=" + text3
			}, TimeSpan.FromMinutes(2.0));
			string text6 = Path.Combine(text3, "project.godot");
			if (!tuple2.Item1 || !File.Exists(text6))
			{
				return (Ok: false, Message: "转换游戏工程配置为文本失败：" + tuple2.Item2, ConfigPath: "");
			}
			List<string> list = File.ReadAllLines(text6).ToList();
			if (!list.Any((string line) => line.StartsWith("DesktopRacerHelperBridge=", StringComparison.Ordinal)))
			{
				int num = list.FindIndex((string line) => line.Trim() == "[autoload]");
				if (num < 0)
				{
					return (Ok: false, Message: "游戏工程配置缺少自动加载段，无法注入桥接脚本。", ConfigPath: "");
				}
				int num2;
				for (num2 = num + 1; num2 < list.Count && list[num2].StartsWith(" ") && list[num2].Trim().Length > 0; num2++)
				{
				}
				list.Insert(num2, "DesktopRacerHelperBridge=\"*res://scripts/app/helper_bridge.gd\"");
				File.WriteAllLines(text6, list);
			}
			(bool, string) tuple3 = RunTool(new string[2]
			{
				"--txt-to-bin=" + text6,
				"--output=" + text4
			}, TimeSpan.FromMinutes(2.0));
			string text7 = Path.Combine(text4, "project.binary");
			if (!tuple3.Item1 || !File.Exists(text7))
			{
				return (Ok: false, Message: "生成游戏工程配置失败：" + tuple3.Item2, ConfigPath: "");
			}
			return (Ok: true, Message: "", ConfigPath: text7);
		}
		catch (Exception ex)
		{
			return (Ok: false, Message: ex.Message, ConfigPath: "");
		}
	}

	private static (bool Ok, string Message) RunTool(string[] arguments, TimeSpan timeout)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(ToolPath)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = ToolDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			processStartInfo.ArgumentList.Add("--headless");
			foreach (string item in arguments)
			{
				processStartInfo.ArgumentList.Add(item);
			}
			using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("无法启动 Godot 项目补丁工具。");
			Task<string> task = process.StandardOutput.ReadToEndAsync();
			Task<string> task2 = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit((int)timeout.TotalMilliseconds))
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch
				{
				}
				return (Ok: false, Message: "GDRE 工具执行超时。");
			}
			Task.WaitAll(task, task2);
			if (process.ExitCode == 0)
			{
				return (Ok: true, Message: "");
			}
			string text = (task2.Result + Environment.NewLine + task.Result).Trim();
			if (text.Length > 900)
			{
				string text2 = text;
				text = text2.Substring(text2.Length - 900);
			}
			return (Ok: false, Message: $"退出码 {process.ExitCode}：{text}");
		}
		catch (Exception ex)
		{
			return (Ok: false, Message: ex.Message);
		}
	}

	private static string ManifestPath(string target)
	{
		return target + ".helper-plugin.json";
	}

	private static void SaveManifest(string path, PluginManifest manifest)
	{
		string text = path + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		File.Move(text, path, overwrite: true);
	}

	private static bool IsProcessUsing(string executable)
	{
		string fullPath = Path.GetFullPath(executable);
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				if (string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? ""), fullPath, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			catch
			{
			}
			finally
			{
				process.Dispose();
			}
		}
		return false;
	}

	private static string GetString(JsonElement root, string key)
	{
		if (!root.TryGetProperty(key, out var value))
		{
			string propertyName = char.ToUpperInvariant(key[0]) + key.Substring(1);
			if (!root.TryGetProperty(propertyName, out value))
			{
				return "";
			}
		}
		return value.GetString() ?? "";
	}

	private static int GetInt(JsonElement root, string key)
	{
		if (!root.TryGetProperty(key, out var value))
		{
			string propertyName = char.ToUpperInvariant(key[0]) + key.Substring(1);
			if (!root.TryGetProperty(propertyName, out value))
			{
				return 0;
			}
		}
		if (!value.TryGetInt32(out var value2))
		{
			return 0;
		}
		return value2;
	}
}
