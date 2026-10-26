using System;
using System.Collections.Generic;

namespace Pintora.Avalonia.Editor.History;

/// <summary>
/// A bounded, single-timeline undo/redo history.
/// The editor supplies immutable mementos; this class only owns timeline semantics.
/// </summary>
public sealed class EditorHistory<T>
{
	private readonly int capacity;
	private readonly List<T> undo = new ();
	private readonly List<T> redo = new ();

	public EditorHistory (int capacity = 100)
	{
		if (capacity < 1)
			throw new ArgumentOutOfRangeException (nameof (capacity));
		this.capacity = capacity;
	}

	public int UndoCount => undo.Count;
	public int RedoCount => redo.Count;
	public int Count => undo.Count;
	public bool CanUndo => undo.Count > 0;
	public bool CanRedo => redo.Count > 0;
	public T PeekUndo () => undo[^1];
	public T Peek () => undo[^1];
	public T PopUndo ()
	{
		T value = undo[^1];
		undo.RemoveAt (undo.Count - 1);
		return value;
	}
	public T Pop () => PopUndo ();
	public void PushRedo (T state) => redo.Add (state);
	public T PopRedo ()
	{
		T value = redo[^1];
		redo.RemoveAt (redo.Count - 1);
		return value;
	}
	public IReadOnlyList<T> UndoItems => undo;

	public void Push (T state)
	{
		undo.Add (state);
		if (undo.Count > capacity)
			undo.RemoveAt (0);
		redo.Clear ();
	}

	public bool TryUndo (T current, out T previous)
	{
		if (!CanUndo) {
			previous = current;
			return false;
		}
		previous = undo[^1];
		undo.RemoveAt (undo.Count - 1);
		redo.Add (current);
		return true;
	}

	public bool TryRedo (T current, out T next)
	{
		if (!CanRedo) {
			next = current;
			return false;
		}
		next = redo[^1];
		redo.RemoveAt (redo.Count - 1);
		undo.Add (current);
		return true;
	}

	public void Clear ()
	{
		undo.Clear ();
		redo.Clear ();
	}
}
