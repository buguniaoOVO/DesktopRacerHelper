using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopRacerHelper;

internal sealed class ActionGate
{
	public static readonly ActionGate Shared = new ActionGate();

	private readonly SemaphoreSlim _serial = new SemaphoreSlim(1, 1);

	private long _nextAt;

	public Func<AppSettings> Settings { get; set; } = AppSettings.Load;

	public int LastIntervalSeconds { get; private set; }

	public async Task ExecuteAsync(Func<Task> action, CancellationToken token)
	{
		await _serial.WaitAsync(token);
		try
		{
			while (Stopwatch.GetTimestamp() < _nextAt)
			{
				double val = (double)(_nextAt - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency;
				await Task.Delay(TimeSpan.FromSeconds(Math.Max(0.01, Math.Min(0.25, val))), token);
			}
			token.ThrowIfCancellationRequested();
			AppSettings appSettings = Settings();
			int num = Math.Clamp(appSettings.DelayMinSeconds, 2, 3600);
			int num2 = Math.Clamp(appSettings.DelayMaxSeconds, num, 3600);
			LastIntervalSeconds = Random.Shared.Next(num, num2 + 1);
			_nextAt = Stopwatch.GetTimestamp() + LastIntervalSeconds * Stopwatch.Frequency;
			try
			{
				await action();
			}
			finally
			{
				_nextAt = Stopwatch.GetTimestamp() + LastIntervalSeconds * Stopwatch.Frequency;
			}
		}
		finally
		{
			_serial.Release();
		}
	}
}
