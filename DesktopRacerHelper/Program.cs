using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopRacerHelper;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		RecoveredApplicationStartup.Initialize();
		if (args.Length == 2 && args[0] == "--background-panel-check")
		{
			Task.Run(() => Diagnostics.BackgroundPanelCheckAsync(args[1])).GetAwaiter().GetResult();
			return;
		}
		if (args.Length == 2 && args[0] == "--background-shot")
		{
			BackgroundCapture.Destination = args[1];
			Application.Run(new MainForm());
			return;
		}
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += (object _, ThreadExceptionEventArgs e) =>
		{
			ReportFatal(e.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += (object _, UnhandledExceptionEventArgs e) =>
		{
			ReportFatal(e.ExceptionObject as Exception);
		};
		TaskScheduler.UnobservedTaskException += (object? _, UnobservedTaskExceptionEventArgs e) =>
		{
			e.SetObserved();
			ReportFatal(e.Exception);
		};
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "Local\\DesktopRacerHelper.Singleton", out createdNew))
		{
			if (!createdNew && BackgroundCapture.Destination == null)
			{
				MessageBox.Show("Desktop Racer 助手已在运行。\nDesktop Racer Helper is already running.", "Desktop Racer 助手", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			else
			{
				Application.Run(new MainForm());
			}
		}
	}

	private static void ReportFatal(Exception? error)
	{
		if (error != null)
		{
			try
			{
				string? directoryName = Path.GetDirectoryName(AppSettings.SettingsPath);
				Directory.CreateDirectory(directoryName);
				File.AppendAllText(Path.Combine(directoryName, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {error}{Environment.NewLine}{Environment.NewLine}");
			}
			catch
			{
			}
			MessageBox.Show("助手遇到未处理的错误，已写入 crash.log：\n" + error.Message, "运行错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}
}
