using System;
using Avalonia.Threading;
using Pinta.Core;

namespace Pintora.Avalonia;

/// <summary>Avalonia implementation of the neutral UI scheduler, backed by the Avalonia dispatcher.</summary>
public sealed class AvaloniaUiScheduler : IUiScheduler
{
	public IDisposable StartTimer (TimeSpan interval, Func<bool> tick)
	{
		var timer = new DispatcherTimer { Interval = interval };
		timer.Tick += (_, _) => {
			if (!tick ())
				timer.Stop ();
		};
		timer.Start ();
		return new TimerHandle (timer);
	}

	public void Post (Action action)
		=> Dispatcher.UIThread.Post (action);

	public void PumpPending ()
		=> Dispatcher.UIThread.RunJobs (DispatcherPriority.Layout);

	private sealed class TimerHandle (DispatcherTimer timer) : IDisposable
	{
		public void Dispose () => timer.Stop ();
	}
}
