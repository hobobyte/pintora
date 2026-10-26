using NUnit.Framework;
using Pintora.Avalonia.Editor.History;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class EditorHistoryTests
{
	[Test]
	public void UndoAndRedoUseOneTimeline ()
	{
		var history = new EditorHistory<int> (10);
		history.Push (1);
		history.Push (2);

		Assert.That (history.TryUndo (3, out int previous), Is.True);
		Assert.That (previous, Is.EqualTo (2));
		Assert.That (history.TryUndo (2, out previous), Is.True);
		Assert.That (previous, Is.EqualTo (1));
		Assert.That (history.TryRedo (1, out int next), Is.True);
		Assert.That (next, Is.EqualTo (2));
	}

	[Test]
	public void PushingAfterUndoClearsRedo ()
	{
		var history = new EditorHistory<int> (10);
		history.Push (1);
		history.TryUndo (2, out _);
		history.Push (3);

		Assert.That (history.CanRedo, Is.False);
	}

	[Test]
	public void HistoryIsBounded ()
	{
		var history = new EditorHistory<int> (2);
		history.Push (1);
		history.Push (2);
		history.Push (3);

		Assert.That (history.UndoCount, Is.EqualTo (2));
		Assert.That (history.TryUndo (4, out int previous), Is.True);
		Assert.That (previous, Is.EqualTo (3));
		Assert.That (history.TryUndo (3, out previous), Is.True);
		Assert.That (previous, Is.EqualTo (2));
	}
}
