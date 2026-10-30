using System;

namespace Pinta.Core;

/// <summary>
/// Platform-neutral UI scheduler for timers and main-loop work.
/// </summary>
public interface IUiScheduler
{
	/// <summary>Starts a repeating timer that ticks on the UI thread. Returning false from tick stops it.</summary>
	IDisposable StartTimer (TimeSpan interval, Func<bool> tick);

	/// <summary>Posts an action to run on the UI thread when it is idle.</summary>
	void Post (Action action);

	/// <summary>
	/// Processes currently-pending UI work synchronously. Only for rare cases where a layout pass
	/// must complete before continuing on the calling code path; prefer <see cref="Post"/>.
	/// </summary>
	void PumpPending ();
}
