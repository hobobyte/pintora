using System;
using GLib;
using TimeSpan = System.TimeSpan;

namespace Pinta.Core;

/// <summary>GTK implementation of the neutral UI scheduler, backed by the GLib main loop.</summary>
public sealed class GtkUiScheduler : IUiScheduler
{
	public IDisposable StartTimer (TimeSpan interval, Func<bool> tick)
	{
		uint id = Functions.TimeoutAdd (
			0,
			(uint) Math.Max (1, interval.TotalMilliseconds),
			() => tick ());
		return new TimerHandle (id);
	}

	public void Post (Action action)
		=> Functions.IdleAdd (0, () => { action (); return false; });

	public void PumpPending ()
		=> MainContext.Default ().Iteration (false);

	private sealed class TimerHandle (uint timerId) : IDisposable
	{
		public void Dispose ()
		{
			if (timerId != 0)
				Source.Remove (timerId);
		}
	}
}
