using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace DesktopRacerHelper;

internal static class BackgroundCapture
{
	internal enum Mode
	{
		Off,
		Requested,
		Running,
		Finished
	}

	private const uint PW_RENDERFULLCONTENT = 2u;

	public static string? Destination { get; set; }

	public static Mode State { get; set; }

	public static string[] Pages { get; } = new string[7] { "overview", "warehouse", "chest", "quality", "settings", "environment", "log" };

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

	[DllImport("user32.dll")]
	private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

	public static bool CaptureControl(System.Windows.Forms.Control control, string path)
	{
		try
		{
			int width = Math.Max(1, control.Width);
			int height = Math.Max(1, control.Height);
			string? directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
			using Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			control.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
			bitmap.Save(path, ImageFormat.Png);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool CaptureWindow(IntPtr hwnd, string path)
	{
		if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out Rect rect))
		{
			return false;
		}
		int width = rect.Right - rect.Left;
		int height = rect.Bottom - rect.Top;
		if (width <= 0 || height <= 0)
		{
			return false;
		}
		try
		{
			string? directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
			using Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				IntPtr hdc = graphics.GetHdc();
				bool flag;
				try
				{
					flag = PrintWindow(hwnd, hdc, 2u);
				}
				finally
				{
					graphics.ReleaseHdc(hdc);
				}
				if (!flag)
				{
					return false;
				}
			}
			bitmap.Save(path, ImageFormat.Png);
			return true;
		}
		catch
		{
			return false;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}
}
