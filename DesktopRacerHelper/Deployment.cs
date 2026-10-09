using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DesktopRacerHelper;

internal sealed class Deployment
{
	public const string CalibratedEngineVersion = "4.7.1";

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public static string Folder => Path.GetDirectoryName(AppSettings.SettingsPath);

	public static string BridgeFolder => Path.Combine(Folder, "bridge");

	public static string RecordPath => Path.Combine(Folder, "environment.json");

	public static GameFingerprint? Inspect(string? executablePath)
	{
		if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
		{
			return null;
		}
		FileInfo fileInfo = new FileInfo(executablePath);
		GameFingerprint gameFingerprint = new GameFingerprint
		{
			ExecutablePath = PluginInstall.CanonicalGameExecutablePath(executablePath),
			Size = fileInfo.Length
		};
		try
		{
			using SHA256 sHA = SHA256.Create();
			using FileStream fileStream = File.OpenRead(executablePath);
			byte[] array = new byte[4194304];
			byte[] carry = Array.Empty<byte>();
			string text = "";
			int num;
			while ((num = fileStream.Read(array, 0, array.Length)) > 0)
			{
				sHA.TransformBlock(array, 0, num, null, 0);
				if (text.Length == 0)
				{
					text = FindBanner(carry, array, num);
				}
				int num2 = Math.Min(num, 64);
				carry = array[(num - num2)..num];
			}
			sHA.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			gameFingerprint.Sha256 = Convert.ToHexString(sHA.Hash).ToLowerInvariant();
			gameFingerprint.Engine = ((text.Length > 0) ? "Godot" : DetectOtherEngine(executablePath));
			gameFingerprint.Suffix = text;
		}
		catch
		{
			gameFingerprint.Engine = "unknown";
		}
		return gameFingerprint;
	}

	private static string FindBanner(byte[] carry, byte[] buffer, int length)
	{
		byte[] array = new byte[carry.Length + length];
		Buffer.BlockCopy(carry, 0, array, 0, carry.Length);
		Buffer.BlockCopy(buffer, 0, array, carry.Length, length);
		string text = Encoding.ASCII.GetString(array);
		int num = text.IndexOf("Godot Engine v", StringComparison.Ordinal);
		if (num < 0)
		{
			return "";
		}
		int num2 = num + "Godot Engine v".Length;
		int i;
		for (i = num2; i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '.'); i++)
		{
		}
		int num3 = num2;
		return text.Substring(num3, i - num3);
	}

	private static string DetectOtherEngine(string path)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(path);
			if (File.Exists(Path.Combine(directoryName, "UnityPlayer.dll")))
			{
				return "Unity";
			}
			if (File.Exists(Path.Combine(directoryName, "GameAssembly.dll")))
			{
				return "Unity";
			}
			return "unknown";
		}
		catch
		{
			return "unknown";
		}
	}

	public static string HashFile(string path)
	{
		using FileStream source = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
	}

	public static EnvironmentRecord? LoadRecord()
	{
		try
		{
			if (!File.Exists(RecordPath))
			{
				return null;
			}
			return JsonSerializer.Deserialize<EnvironmentRecord>(File.ReadAllText(RecordPath));
		}
		catch
		{
			return null;
		}
	}

	public static DeployResult Deploy(GameFingerprint fingerprint, int bridgePort, string bridgeMode)
	{
		try
		{
			Directory.CreateDirectory(Folder);
			Directory.CreateDirectory(BridgeFolder);
			EnvironmentRecord value = new EnvironmentRecord
			{
				GamePath = fingerprint.ExecutablePath,
				ExeSha256 = fingerprint.Sha256,
				ExeSize = fingerprint.Size,
				Engine = fingerprint.Engine + (string.IsNullOrEmpty(fingerprint.Suffix) ? "" : (" " + fingerprint.Suffix)),
				DeployedAt = DateTime.Now,
				BridgePort = bridgePort,
				BridgeMode = bridgeMode
			};
			File.WriteAllText(RecordPath, JsonSerializer.Serialize(value, Options));
			return new DeployResult(Ok: true, "deploy.done");
		}
		catch (Exception ex)
		{
			return new DeployResult(Ok: false, ex.Message);
		}
	}

	public static DeployResult Purge()
	{
		try
		{
			if (Directory.Exists(BridgeFolder))
			{
				Directory.Delete(BridgeFolder, recursive: true);
			}
			if (File.Exists(RecordPath))
			{
				File.Delete(RecordPath);
			}
			return new DeployResult(Ok: true, "purge.done");
		}
		catch (Exception ex)
		{
			return new DeployResult(Ok: false, ex.Message);
		}
	}

	public static string Verify(GameFingerprint? current)
	{
		EnvironmentRecord environmentRecord = LoadRecord();
		if (environmentRecord == null)
		{
			return "verify.notDeployed";
		}
		if (current == null)
		{
			return "verify.gameMissing";
		}
		if (!string.Equals(environmentRecord.ExeSha256, current.Sha256, StringComparison.OrdinalIgnoreCase))
		{
			return "verify.hashChanged";
		}
		if (current.Engine == "Godot")
		{
			string text = MajorVersion(current.Suffix);
			if (text.Length > 0 && text != "4.7.1")
			{
				return "verify.engineChanged";
			}
		}
		return "";
	}

	private static string MajorVersion(string suffix)
	{
		if (string.IsNullOrEmpty(suffix))
		{
			return "";
		}
		string[] array = suffix.Split('.');
		List<string> list = new List<string>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text.Length <= 0 || !text.All(char.IsDigit))
			{
				break;
			}
			list.Add(text);
		}
		return string.Join(".", list);
	}
}
