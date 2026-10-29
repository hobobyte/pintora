using System;

namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="ITextInputContext"/> based on Gtk.IMMulticontext.</summary>
public sealed class GtkTextInputContext : ITextInputContext
{
	private readonly Gtk.IMMulticontext imContext;

	public GtkTextInputContext ()
	{
		imContext = Gtk.IMMulticontext.New ();
		imContext.OnCommit += (o, args) => Commit?.Invoke (this, args.Str);
		imContext.OnPreeditStart += (o, args) => PreeditStart?.Invoke (this, EventArgs.Empty);
		imContext.OnPreeditChanged += (o, args) => PreeditChanged?.Invoke (this, EventArgs.Empty);
		imContext.OnPreeditEnd += (o, args) => PreeditEnd?.Invoke (this, EventArgs.Empty);
	}

	public void FocusIn () => imContext.FocusIn ();

	public void FocusOut () => imContext.FocusOut ();

	public void SetClientWidget (object? nativeWidget)
		=> imContext.SetClientWidget ((Gtk.Widget?) nativeWidget);

	public bool FilterKeyPress (object nativeKeyEvent)
		=> imContext.FilterKeypress ((Gdk.Event) nativeKeyEvent);

	public void Reset () => imContext.Reset ();

	public string GetPreeditString ()
	{
		imContext.GetPreeditString (out string str, out _, out _);
		return str;
	}

	public event EventHandler<string>? Commit;
	public event EventHandler? PreeditStart;
	public event EventHandler? PreeditChanged;
	public event EventHandler? PreeditEnd;
}

/// <summary>Creates <see cref="GtkTextInputContext"/> instances.</summary>
public sealed class GtkTextInputContextFactory : ITextInputContextFactory
{
	public ITextInputContext CreateInputContext ()
		=> new GtkTextInputContext ();
}
