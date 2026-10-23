using System;
using System.Collections.Generic;
using Avalonia.Input;
using Pinta.Core;

namespace Pintora.Avalonia.Input;

public sealed record PintoraKeyCommand (string Id, string DefaultGesture);

public static class PintoraKeyCommands
{
	public static readonly PintoraKeyCommand Undo = new ("edit.undo", "Ctrl+Z");
	public static readonly PintoraKeyCommand Redo = new ("edit.redo", "Ctrl+Y");
	public static readonly PintoraKeyCommand DecreaseBrush = new ("brush.decrease", "[");
	public static readonly PintoraKeyCommand IncreaseBrush = new ("brush.increase", "]");
	public static readonly PintoraKeyCommand ToggleEraser = new ("tool.toggle-eraser", "E");

	public static IReadOnlyList<PintoraKeyCommand> All { get; } = [Undo, Redo, DecreaseBrush, IncreaseBrush, ToggleEraser];
}

public sealed class PintoraKeyboardSettings
{
	private readonly ISettingsService? settings;
	private readonly Dictionary<string, string> bindings = new (StringComparer.Ordinal);

	public PintoraKeyboardSettings (ISettingsService? settings = null)
	{
		this.settings = settings;
		foreach (PintoraKeyCommand command in PintoraKeyCommands.All) {
			string key = SettingKey (command);
			string value = settings?.GetSetting (key, command.DefaultGesture) ?? command.DefaultGesture;
			bindings[command.Id] = Normalize (value) ?? command.DefaultGesture;
		}
	}

	public string GestureTextFor (PintoraKeyCommand command)
		=> bindings.TryGetValue (command.Id, out string? value) ? value : command.DefaultGesture;

	public KeyGesture? GestureFor (PintoraKeyCommand command)
		=> Parse (GestureTextFor (command));

	public bool Matches (KeyEventArgs e, PintoraKeyCommand command)
		=> GestureFor (command)?.Matches (e) == true;

	public void SetGesture (PintoraKeyCommand command, string? gesture)
	{
		string value = Normalize (gesture) ?? string.Empty;
		if (settings is not null)
			settings.PutSetting (SettingKey (command), value);
		bindings[command.Id] = value;
	}

	private static string SettingKey (PintoraKeyCommand command) => $"Pintora.Keyboard.{command.Id}";

	private static KeyGesture? Parse (string? text)
	{
		if (string.IsNullOrWhiteSpace (text)) return null;
		string[] parts = text.Split ('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 0) return null;
		KeyModifiers modifiers = KeyModifiers.None;
		string keyText = parts[^1];
		foreach (string part in parts[..^1]) {
			if (part.Equals ("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= KeyModifiers.Control;
			else if (part.Equals ("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= KeyModifiers.Shift;
			else if (part.Equals ("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= KeyModifiers.Alt;
			else return null;
		}
		if (keyText == "[") keyText = nameof (Key.OemOpenBrackets);
		if (keyText == "]") keyText = nameof (Key.OemCloseBrackets);
		if (!Enum.TryParse (keyText, true, out Key key)) return null;
		return new KeyGesture (key, modifiers);
	}

	private static string? Normalize (string? text)
	{
		KeyGesture? gesture = Parse (text);
		if (gesture is null) return string.IsNullOrWhiteSpace (text) ? string.Empty : null;
		string modifiers = gesture.KeyModifiers.HasFlag (KeyModifiers.Control) ? "Ctrl+" : string.Empty;
		modifiers += gesture.KeyModifiers.HasFlag (KeyModifiers.Shift) ? "Shift+" : string.Empty;
		modifiers += gesture.KeyModifiers.HasFlag (KeyModifiers.Alt) ? "Alt+" : string.Empty;
		string key = gesture.Key switch {
			Key.OemOpenBrackets => "[",
			Key.OemCloseBrackets => "]",
			_ => gesture.Key.ToString ()
		};
		return modifiers + key;
	}
}
