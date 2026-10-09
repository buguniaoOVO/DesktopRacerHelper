using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopRacerHelper;

internal static class Native
{
	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;

		public readonly int Width => Right - Left;

		public readonly int Height => Bottom - Top;
	}

	private delegate bool EnumProc(nint handle, nint data);

	internal sealed record WindowInfo(nint Handle, string Title, string ClassName, int ProcessId, int Width, int Height);

	public const int SwRestore = 9;

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumProc callback, nint data);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint handle);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowTextW(nint handle, StringBuilder text, int count);

	[DllImport("user32.dll")]
	private static extern int GetWindowTextLengthW(nint handle);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassNameW(nint handle, StringBuilder text, int count);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(nint handle, out Rect rect);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint handle, int command);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint handle);

	public static List<WindowInfo> EnumerateWindows()
	{
		List<WindowInfo> result = new List<WindowInfo>();
		EnumWindows((nint handle, nint _) =>
		{
			if (!IsWindowVisible(handle))
			{
				return true;
			}
			int windowTextLengthW = GetWindowTextLengthW(handle);
			if (windowTextLengthW <= 0)
			{
				return true;
			}
			StringBuilder stringBuilder = new StringBuilder(windowTextLengthW + 1);
			GetWindowTextW(handle, stringBuilder, stringBuilder.Capacity);
			StringBuilder stringBuilder2 = new StringBuilder(256);
			GetClassNameW(handle, stringBuilder2, stringBuilder2.Capacity);
			GetWindowThreadProcessId(handle, out var processId);
			GetClientRect(handle, out var rect);
			result.Add(new WindowInfo(handle, stringBuilder.ToString(), stringBuilder2.ToString(), (int)processId, rect.Width, rect.Height));
			return true;
		}, IntPtr.Zero);
		return result;
	}

	public static void Focus(nint handle)
	{
		StringBuilder stringBuilder = new StringBuilder(128);
		GetClassNameW(handle, stringBuilder, stringBuilder.Capacity);
		if (!stringBuilder.ToString().StartsWith("WindowsForms10", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("仅允许从托盘恢复助手自身窗口。");
		}
		ShowWindow(handle, 9);
		SetForegroundWindow(handle);
	}
}
