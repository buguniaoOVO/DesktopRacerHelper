using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DesktopRacerHelper;

internal static class ItemCatalog
{
	public static readonly IReadOnlyList<string> Families = new _003C_003Ez__ReadOnlyArray<string>(new string[36]
	{
		"Aegiron", "Aeonexus", "Aerion", "Aetherion", "Apexion", "Atlasyn", "Chronex", "Cirquon", "Dampiron", "Eternyx",
		"Flexaris", "Gaiaron", "Gravon", "Ignivar", "Kairon", "Neuraxis", "Nexaris", "Omnisyn", "Prometheon", "Quantaris",
		"Rydan", "Solkaris", "Stabilon", "Stratovex", "Synaptix", "Syncron", "Tempestra", "Terravax", "Torqen", "Traxen",
		"Vantis", "Velkar", "Veltrix", "Ventaris", "Vorteron", "Zephyra"
	});

	public static readonly IReadOnlyList<string> Archetypes = new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "Anchor", "Flow", "Rush", "Surge", "Vector" });

	public static string Correct(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return name;
		}
		string text = string.Empty;
		string text2 = name;
		if (GameVision.IsMarketable(text2))
		{
			text = " [M]";
			text2 = Regex.Replace(text2, "\\s*\\[\\s*M\\s*\\]\\s*[lI1|]?\\s*$", "", RegexOptions.IgnoreCase);
		}
		string text3 = GameVision.Key(text2);
		if (text3.Length == 0)
		{
			return name;
		}
		foreach (string family in Families)
		{
			foreach (string archetype in Archetypes)
			{
				string text4 = GameVision.Key(family + archetype);
				if (text3 == text4)
				{
					return family + " " + archetype + text;
				}
			}
		}
		double num = 0.0;
		string text5 = string.Empty;
		foreach (string family2 in Families)
		{
			foreach (string archetype2 in Archetypes)
			{
				string text6 = family2 + " " + archetype2;
				double num2 = Similarity(text2, text6);
				if (num2 > num)
				{
					num = num2;
					text5 = text6;
				}
			}
		}
		if (!(num >= 0.82))
		{
			return name;
		}
		return text5 + text;
	}

	private static double Similarity(string a, string b)
	{
		a = GameVision.Key(a);
		b = GameVision.Key(b);
		if (a.Length == 0 || b.Length == 0)
		{
			return 0.0;
		}
		int[] array = Enumerable.Range(0, b.Length + 1).ToArray();
		for (int i = 1; i <= a.Length; i++)
		{
			int num = array[0];
			array[0] = i;
			for (int j = 1; j <= b.Length; j++)
			{
				int num2 = array[j];
				array[j] = Math.Min(Math.Min(array[j] + 1, array[j - 1] + 1), num + ((a[i - 1] != b[j - 1]) ? 1 : 0));
				num = num2;
			}
		}
		return 1.0 - (double)array[b.Length] / (double)Math.Max(a.Length, b.Length);
	}
}
