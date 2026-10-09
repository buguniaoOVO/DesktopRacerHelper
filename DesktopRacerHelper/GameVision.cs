using System.Text.RegularExpressions;

namespace DesktopRacerHelper;

internal static class GameVision
{
	public static string Key(string name)
	{
		return Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9\\[\\]+]", "");
	}

	public static bool IsMarketable(string name)
	{
		return Regex.IsMatch(name, "\\[\\s*M\\s*\\]\\s*[lI1|]?\\s*$", RegexOptions.IgnoreCase);
	}

	public static bool IsConsumable(string name)
	{
		return Regex.IsMatch(name, "\\bDTR\\s*C[o0]in\\b", RegexOptions.IgnoreCase);
	}

	public static bool Protected(string name)
	{
		if (!IsMarketable(name))
		{
			return IsConsumable(name);
		}
		return true;
	}

	public static string CleanName(string raw)
	{
		string text = Regex.Replace(raw ?? "", "\\s+", " ").Trim();
		if (IsConsumable(text))
		{
			if (!IsMarketable(text))
			{
				return "DTR Coin";
			}
			return "DTR Coin [M]";
		}
		if (IsMarketable(text))
		{
			text = Regex.Replace(text, "\\s*\\[\\s*M\\s*\\]\\s*[lI1|]?\\s*$", " [M]", RegexOptions.IgnoreCase);
		}
		return text;
	}
}
