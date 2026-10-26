using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Pinta.Core;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia.Document;

/// <summary>How a dirty tab should be handled when the user closes it.</summary>
public enum CloseDecision
{
	/// <summary>Persist the document, then close it if the save succeeds.</summary>
	Save,
	/// <summary>Close the tab and drop unsaved changes.</summary>
	Discard,
	/// <summary>Keep the tab open.</summary>
	Cancel,
}

/// <summary>
/// A single document tab: the canvas that renders the document and the
/// session that owns its dirty flag, file path and title.
/// </summary>
public sealed class DocumentTab : INotifyPropertyChanged
{
	private readonly EventHandler session_state_changed;
	private bool released;

	internal DocumentTab (CanvasHost canvas, DocumentSession session)
	{
		Canvas = canvas ?? throw new ArgumentNullException (nameof (canvas));
		Session = session ?? throw new ArgumentNullException (nameof (session));
		session_state_changed = (_, _) => OnSessionStateChanged ();
		Session.StateChanged += session_state_changed;
	}

	/// <summary>The viewport that renders this document.</summary>
	public CanvasHost Canvas { get; }

	/// <summary>The per-document aggregate (dirty flag, path, title).</summary>
	public DocumentSession Session { get; }

	/// <summary>Display title, suitable for a <c>TabItem.Header</c> binding.</summary>
	public string Title => Session.Title;

	/// <summary>True when the document has unsaved changes.</summary>
	public bool IsDirty => Session.IsDirty;

	/// <summary>
	/// The text to show in a tab header: <see cref="Title"/>, suffixed with
	/// <c>" *"</c> when the document has unsaved changes.
	/// </summary>
	public string DisplayTitle => IsDirty ? Title + " *" : Title;

	/// <summary>Raised when <see cref="Title"/> or <see cref="IsDirty"/> change.</summary>
	public event EventHandler? StateChanged;

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	internal void Detach ()
		=> Session.StateChanged -= session_state_changed;

	internal void Release (Action<DocumentTab>? release)
	{
		if (released)
			return;
		released = true;
		Detach ();
		release?.Invoke (this);
	}

	private void OnSessionStateChanged ()
	{
		StateChanged?.Invoke (this, EventArgs.Empty);
		PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (nameof (DisplayTitle)));
		PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (nameof (Title)));
		PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (nameof (IsDirty)));
	}
}

/// <summary>
/// Owns the open document tabs for the multi-document plan (Slice 2).
/// Each tab pairs an independent <see cref="CanvasHost"/> with its
/// <see cref="DocumentSession"/>, so layers, undo history and dirty state
/// are isolated per document.
/// </summary>
/// <remarks>
/// The manager is UI-agnostic: <see cref="Tabs"/> is an
/// <see cref="ObservableCollection{T}"/> that a <c>TabControl</c> can bind to,
/// and <see cref="ActiveTab"/> drives the selected item. Closing a dirty tab
/// requires an explicit <see cref="CloseDecision"/>; the save delegate is
/// injected so the manager never touches the file system itself.
/// </remarks>
public sealed class DocumentTabManager : IDisposable
{
	private readonly ObservableCollection<DocumentTab> tabs = new ();
	private readonly Func<DocumentTab, bool> save;
	private readonly Action<DocumentTab>? release;
	private DocumentTab? active_tab;
	private bool disposed;

	/// <param name="save">
	/// Persists a tab's document and returns <c>true</c> on success. Invoked
	/// only for <see cref="CloseDecision.Save"/> on a dirty tab. Throwing or
	/// returning <c>false</c> keeps the tab open.
	/// </param>
	public DocumentTabManager (Func<DocumentTab, bool> save, Action<DocumentTab>? release = null)
	{
		this.save = save ?? throw new ArgumentNullException (nameof (save));
		this.release = release;
	}

	/// <summary>The open tabs, in display order.</summary>
	public ObservableCollection<DocumentTab> Tabs => tabs;

	/// <summary>The currently selected tab, or <c>null</c> when none are open.</summary>
	public DocumentTab? ActiveTab => active_tab;

	/// <summary>Raised when <see cref="ActiveTab"/> changes.</summary>
	public event EventHandler? ActiveTabChanged;

	/// <summary>Creates a blank document tab and makes it active.</summary>
	public DocumentTab AddNewDocument (int width = 800, int height = 600)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException (nameof (width), "Document dimensions must be positive.");
		var canvas = new CanvasHost (new Pinta.Core.Size (width, height));
		return AddTab (canvas, canvas.DocumentSession);
	}

	/// <summary>
	/// Opens an existing document from <paramref name="path"/> and makes it
	/// active. The tab starts clean with the file's name as its title.
	/// </summary>
	public DocumentTab AddExistingDocument (string path)
	{
		if (string.IsNullOrWhiteSpace (path))
			throw new ArgumentException ("A document path is required.", nameof (path));
		var canvas = new CanvasHost ();
		try {
			canvas.OpenImage (path);
		} catch {
			canvas.Dispose ();
			throw;
		}
		return AddTab (canvas, canvas.DocumentSession);
	}

	/// <summary>Adds an already-constructed tab and makes it active.</summary>
	public DocumentTab AddTab (CanvasHost canvas, DocumentSession session)
	{
		var tab = new DocumentTab (canvas, session);
		tabs.Add (tab);
		SetActiveTab (tab);
		return tab;
	}

	/// <summary>Selects the tab at <paramref name="index"/>.</summary>
	public bool SwitchTo (int index)
	{
		if ((uint) index >= (uint) tabs.Count)
			return false;
		SetActiveTab (tabs[index]);
		return true;
	}

	/// <summary>
	/// Closes the tab at <paramref name="index"/> according to
	/// <paramref name="decision"/>. Returns <c>true</c> when the tab was
	/// removed. A dirty tab is only removed for <see cref="CloseDecision.Discard"/>
	/// or a successful <see cref="CloseDecision.Save"/>.
	/// </summary>
	public bool CloseTab (int index, CloseDecision decision)
	{
		if ((uint) index >= (uint) tabs.Count)
			return false;
		if (!Enum.IsDefined (decision))
			throw new ArgumentOutOfRangeException (nameof (decision));

		DocumentTab tab = tabs[index];
		if (decision == CloseDecision.Cancel)
			return false;

		if (tab.IsDirty) {
			if (decision == CloseDecision.Save) {
				bool saved;
				try {
					saved = save (tab);
				} catch {
					// A failed save must leave the tab open and intact.
					return false;
				}
				if (!saved)
					return false;
			}
		}

		RemoveTab (index);
		return true;
	}

	/// <summary>Closes the active tab, if any.</summary>
	public bool CloseActiveTab (CloseDecision decision)
	{
		int index = active_tab is null ? -1 : tabs.IndexOf (active_tab);
		return index >= 0 && CloseTab (index, decision);
	}

	private void RemoveTab (int index)
	{
		DocumentTab removed = tabs[index];
		bool was_active = ReferenceEquals (removed, active_tab);
		tabs.RemoveAt (index);
		removed.Release (release);

		if (!was_active)
			return;

		// Deterministic neighbor selection: the tab that shifted into the
		// closed slot, otherwise the one before it, otherwise none.
		DocumentTab? next = tabs.Count == 0
			? null
			: tabs[Math.Min (index, tabs.Count - 1)];
		SetActiveTab (next);
	}

	public void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		// Clear the active tab first so no ActiveTabChanged handler observes a
		// tab that is about to be released, then release each tab exactly once.
		SetActiveTab (null);
		foreach (DocumentTab tab in tabs.ToArray ())
			tab.Release (release);
		tabs.Clear ();
	}

	private void SetActiveTab (DocumentTab? tab)
	{
		if (ReferenceEquals (active_tab, tab))
			return;
		active_tab = tab;
		ActiveTabChanged?.Invoke (this, EventArgs.Empty);
	}
}
