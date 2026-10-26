using System;

namespace Pintora.Avalonia.Editor.Commands;

public sealed record EditorCommand (string Id, string DisplayName, Action Execute, Func<bool>? CanExecute = null)
{
	public bool IsEnabled => CanExecute?.Invoke () ?? true;
}
