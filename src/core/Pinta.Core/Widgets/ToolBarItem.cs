namespace Pinta.Core;

public sealed class ToolBarItem
{
	public ToolBarItem (string text, string imageId) : this (text, imageId, null) { }

	public ToolBarItem (string text, string imageId, object? tag)
	{
		Text = text;
		ImageId = imageId;
		Tag = tag;
	}

	public string ImageId { get; }
	public object? Tag { get; }
	public string Text { get; }

	public T GetTagOrDefault<T> (T defaultValue)
		=> Tag is T value ? value : defaultValue;
}
