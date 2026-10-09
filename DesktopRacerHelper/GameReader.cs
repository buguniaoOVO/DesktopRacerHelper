using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace DesktopRacerHelper;

internal static class GameReader
{
	public static Native.WindowInfo? FindGameWindow()
	{
		return Native.EnumerateWindows().FirstOrDefault((Native.WindowInfo w) => w.ClassName == "Engine" && w.Width > 200 && w.Height > 100 && (w.Title.Equals("Desk Top Racer", StringComparison.OrdinalIgnoreCase) || w.Title.Equals("Desktop Racer", StringComparison.OrdinalIgnoreCase)));
	}

	public static Bitmap Fit(Image source, int size)
	{
		Bitmap bitmap = new Bitmap(size, size);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.DrawImage(source, 0, 0, size, size);
		return bitmap;
	}
}
