using System;

namespace Pinta.Core;

/// <summary>
/// Minimal neutral text input method (IME) context, used by tools that edit
/// text in-place on the canvas (e.g. the text tool) to route key events through
/// the platform input method for CJK / compose input.
/// Key events are passed as opaque native event objects (see ToolKeyEventArgs.Event).
/// </summary>
public interface ITextInputContext
{
	/// <summary>Notify the input method that the text widget has gained focus.</summary>
	void FocusIn ();

	/// <summary>Notify the input method that the text widget has lost focus.</summary>
	void FocusOut ();

	/// <summary>Attach the input method to a native canvas widget (null to detach).</summary>
	void SetClientWidget (object? nativeWidget);

	/// <summary>Let the input method consume a native key event. Returns true if it was handled.</summary>
	bool FilterKeyPress (object nativeKeyEvent);

	/// <summary>Reset the input method state, discarding any pre-edit text.</summary>
	void Reset ();

	/// <summary>The current pre-edit (in-progress composition) string.</summary>
	string GetPreeditString ();

	/// <summary>Raised when the input method commits a finalized string.</summary>
	event EventHandler<string>? Commit;

	event EventHandler? PreeditStart;
	event EventHandler? PreeditChanged;
	event EventHandler? PreeditEnd;
}
