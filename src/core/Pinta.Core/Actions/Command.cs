//
// Command.cs
//
// Author:
//       Cameron White <cameronwhite91@gmail.com>
//
// Copyright (c) 2010 Cameron White
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
<<<<<<< HEAD
=======
using GObject;
>>>>>>> origin/master

namespace Pinta.Core;

/// <summary>
<<<<<<< HEAD
/// Platform-neutral application command (name, label, shortcuts, sensitivity, activation).
/// The platform action object (e.g. Gio.SimpleAction) is attached by the
/// <see cref="IActionRegistrar"/> at registration time; neutral code never sees it.
/// </summary>
public class Command
{
	/// <summary>The platform-native action object (e.g. Gio.SimpleAction), set by the registrar.</summary>
	public object? NativeAction { get; internal set; }

	public string Name { get; }
	public event EventHandler? Activated;

	public virtual void Activate ()
	{
		Activated?.Invoke (this, EventArgs.Empty);
=======
/// Wrapper around a Glib.SimpleAction to store additional data such as the label.
/// </summary>
public class Command
{
	public Gio.SimpleAction Action { get; }
	public string Name => Action.Name!;
	public event SignalHandler<Gio.SimpleAction, Gio.SimpleAction.ActivateSignalArgs>? Activated;
	public void Activate ()
	{
		Action.Activate (null);
>>>>>>> origin/master
	}

	public string Label { get; }
	public string? ShortLabel { get; init; }
	public string? Tooltip { get; }
	public string? IconName { get; }
	public string FullName => $"app.{Name}";
	public bool IsImportant { get; } = false;
<<<<<<< HEAD

	private bool sensitive = true;
	public bool Sensitive {
		get => sensitive;
		set {
			if (value == sensitive) return;
			sensitive = value;
			SensitivityChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	/// <summary>Raised when <see cref="Sensitive"/> changes; the platform registrar mirrors it onto the native action.</summary>
	public event EventHandler? SensitivityChanged;

=======
	public bool Sensitive {
		get => Action.Enabled;
		set => Action.Enabled = value;
	}
>>>>>>> origin/master
	public ImmutableArray<string> Shortcuts { get; }

	public Command (
		string name,
		string label,
		string? tooltip,
		string? icon_name,
<<<<<<< HEAD
		IReadOnlyList<string>? shortcuts = null)
	{
		Name = name;
=======
		GLib.Variant? state = null,
		IReadOnlyList<string>? shortcuts = null)
	{
		Action =
			state is null
			? Gio.SimpleAction.New (name, null)
			: Gio.SimpleAction.NewStateful (name, null, state);

		Action.OnActivate += (o, args) => Activated?.Invoke (o, args);

>>>>>>> origin/master
		Label = label;
		Tooltip = tooltip;
		IconName = icon_name;

		Shortcuts =
			shortcuts is null
			? []
			: [.. shortcuts];
	}

<<<<<<< HEAD
	public CommandMenuItem CreateMenuItem ()
		=> new (Label, FullName);
=======
	public Gio.MenuItem CreateMenuItem ()
		=> Gio.MenuItem.New (Label, FullName);
>>>>>>> origin/master
}

public sealed class ToggleCommand : Command
{
	public ToggleCommand (
		string name,
		string label,
		string? tooltip,
		string? stock_id,
		IReadOnlyList<string>? shortcuts = null
	)
		: base (
			name,
			label,
			tooltip,
			stock_id,
<<<<<<< HEAD
=======
			GLib.Variant.NewBoolean (false),
>>>>>>> origin/master
			shortcuts
		)
	{
		Activated += (_, _) => {
<<<<<<< HEAD
			bool active = !value;
			Toggled?.Invoke (active, interactive: true);
			value = active;
		};
	}

	private bool value;
	public bool Value {
		get => value;
		set {
			if (value == this.value) return;
			Toggled?.Invoke (value, interactive: false);
			this.value = value;
=======
			bool active = !State.GetBoolean ();
			Toggled?.Invoke (active, interactive: true);
			Action.ChangeState (GLib.Variant.NewBoolean (active));
		};
	}

	public bool Value {
		get => State.GetBoolean ();
		set {
			if (value == Value) return;
			Toggled?.Invoke (value, interactive: false);
			Action.ChangeState (GLib.Variant.NewBoolean (value));
>>>>>>> origin/master
		}
	}

	public delegate void ToggledHandler (bool value, bool interactive);
	public event ToggledHandler? Toggled;
<<<<<<< HEAD
}

/// <summary>Neutral description of a menu entry bound to an action id.</summary>
public sealed record CommandMenuItem (string Label, string ActionId);
=======

	private GLib.Variant State => Action.GetState () ?? throw new InvalidOperationException ("Action should not be stateless!");
}
>>>>>>> origin/master
