using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DesktopRacerHelper;

internal static class Theme
{
	private static readonly object FontLock = new object();

	private static readonly Dictionary<(string Family, float Size, FontStyle Style), Font> FontCache = new Dictionary<(string Family, float Size, FontStyle Style), Font>();

	public static readonly Color PageBg = Color.FromArgb(246, 248, 252);

	public static readonly Color Surface = Color.FromArgb(255, 255, 255);

	public static readonly Color SurfaceAlt = Color.FromArgb(241, 245, 250);

	public static readonly Color Border = Color.FromArgb(222, 229, 238);

	public static readonly Color BorderStrong = Color.FromArgb(203, 213, 226);

	public static readonly Color Text = Color.FromArgb(27, 36, 48);

	public static readonly Color Muted = Color.FromArgb(103, 120, 139);

	public static readonly Color Accent = Color.FromArgb(31, 111, 235);

	public static readonly Color AccentSoft = Color.FromArgb(232, 240, 254);

	public static readonly Color Warning = Color.FromArgb(198, 106, 40);

	public static readonly Color Good = Color.FromArgb(31, 138, 96);

	public static readonly IReadOnlyList<QualityStyle> Qualities = new _003C_003Ez__ReadOnlyArray<QualityStyle>(new QualityStyle[7]
	{
		new QualityStyle("普通", Color.FromArgb(144, 154, 162), "#909AA2"),
		new QualityStyle("优秀", Color.FromArgb(76, 172, 119), "#4CAC77"),
		new QualityStyle("稀有", Color.FromArgb(77, 138, 179), "#4D8AB3"),
		new QualityStyle("史诗", Color.FromArgb(156, 111, 205), "#9C6FCD"),
		new QualityStyle("传说", Color.FromArgb(196, 142, 74), "#C48E4A"),
		new QualityStyle("神话", Color.FromArgb(203, 79, 129), "#CB4F81"),
		new QualityStyle("异域", Color.FromArgb(204, 62, 62), "#CC3E3E")
	});

	public static readonly QualityStyle Consumable = new QualityStyle("消耗品", Color.FromArgb(56, 180, 199), "#38B4C7");

	public static readonly QualityStyle Unknown = new QualityStyle("未知", Muted, "#67788B");

	public static Font UiFont(FontFamily family, float size, FontStyle style = FontStyle.Regular)
	{
		return UiFont(family.Name, size, style);
	}

	public static Font UiFont(string family, float size, FontStyle style = FontStyle.Regular)
	{
		(string Family, float Size, FontStyle Style) key = (family, size, style);
		lock (FontLock)
		{
			if (!FontCache.TryGetValue(key, out Font font))
			{
				font = new Font(family, size, style);
				FontCache.Add(key, font);
			}
			return font;
		}
	}

	public static GraphicsPath Rounded(Rectangle bounds, int radius)
	{
		GraphicsPath path = new GraphicsPath();
		int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
		if (diameter <= 0)
		{
			path.AddRectangle(bounds);
			return path;
		}
		path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180f, 90f);
		path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270f, 90f);
		path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
		path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
		path.CloseFigure();
		return path;
	}

	public static Color ColorOf(string quality)
	{
		if (string.Equals(quality, Consumable.Name, StringComparison.Ordinal))
		{
			return Consumable.Color;
		}
		if (string.Equals(quality, Unknown.Name, StringComparison.Ordinal))
		{
			return Unknown.Color;
		}
		foreach (QualityStyle quality2 in Qualities)
		{
			if (string.Equals(quality2.Name, quality, StringComparison.Ordinal))
			{
				return quality2.Color;
			}
		}
		return Muted;
	}

	public static string NearestQualityName(Color c)
	{
		QualityStyle qualityStyle = Qualities[0];
		double num = double.MaxValue;
		foreach (QualityStyle quality in Qualities)
		{
			double num2 = Math.Pow(quality.Color.R - c.R, 2.0) + Math.Pow(quality.Color.G - c.G, 2.0) + Math.Pow(quality.Color.B - c.B, 2.0);
			if (num2 < num)
			{
				num = num2;
				qualityStyle = quality;
			}
		}
		return qualityStyle.Name;
	}
}
