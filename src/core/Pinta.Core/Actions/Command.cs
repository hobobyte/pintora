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

namespace Pinta.Core;

/// <summary>
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
	}

	public string Label { get; }
	public string? ShortLabel { get; init; }
	public string? Tooltip { get; }
	public string? IconName { get; }
	public string FullName => $"app.{Name}";
	public bool IsImportant { get; } = false;
	public bool Sensitive { get; set; }
	public ImmutableArray<string> Shortcuts { get; }

	public Command (
		string name,
		string label,
		string? tooltip,
		string? icon_name,
		IReadOnlyList<string>? shortcuts = null)
	{
		Name = name;
		Label = label;
		Tooltip = tooltip;
		IconName = icon_name;

		Shortcuts =
			shortcuts is null
			? []
			: [.. shortcuts];
	}

	public CommandMenuItem CreateMenuItem ()
		=> new (Label, FullName);
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
			shortcuts
		)
	{
		Activated += (_, _) => {
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
		}
	}

	public delegate void ToggledHandler (bool value, bool interactive);
	public event ToggledHandler? Toggled;
}

/// <summary>Neutral description of a menu entry bound to an action id.</summary>
public sealed record CommandMenuItem (string Label, string ActionId);
