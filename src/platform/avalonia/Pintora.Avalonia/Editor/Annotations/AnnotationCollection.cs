using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;

namespace Pintora.Avalonia.Editor.Annotations;

/// <summary>Ordered annotations for a document or layer.</summary>
public sealed class AnnotationCollection
{
	private readonly ObservableCollection<Annotation> items = new ();
	private int update_depth;
	private bool update_pending;

	public IReadOnlyList<Annotation> Items => items;
	public int Count => items.Count;
	public event EventHandler? Changed;

	public void Add (Annotation annotation) { items.Add (annotation); NotifyChanged (); }
	public void AddRange (IEnumerable<Annotation> annotations)
	{
		using (BeginUpdate ()) {
			bool added = false;
			foreach (Annotation annotation in annotations) {
				items.Add (annotation);
				added = true;
			}
			if (added)
				update_pending = true;
		}
	}
	public IDisposable BeginUpdate ()
	{
		update_depth++;
		return new UpdateScope (this);
	}
	public bool Remove (Annotation annotation)
	{
		bool removed = items.Remove (annotation);
		if (removed) NotifyChanged ();
		return removed;
	}
	public void Replace (Annotation existing, Annotation replacement)
	{
		int index = items.IndexOf (existing);
		if (index >= 0)
		{
			items[index] = replacement;
			NotifyChanged ();
		}
	}
	public void Clear ()
	{
		if (items.Count == 0) return;
		items.Clear ();
		NotifyChanged ();
	}

	private void NotifyChanged ()
	{
		if (update_depth > 0) {
			update_pending = true;
			return;
		}
		Changed?.Invoke (this, EventArgs.Empty);
	}

	private sealed class UpdateScope (AnnotationCollection owner) : IDisposable
	{
		private bool disposed;
		public void Dispose ()
		{
			if (disposed) return;
			disposed = true;
			if (--owner.update_depth == 0 && owner.update_pending) {
				owner.update_pending = false;
				owner.Changed?.Invoke (owner, EventArgs.Empty);
			}
		}
	}
}
