using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia.Document;

/// <summary>
/// UI-agnostic glue between a <see cref="DocumentTabManager"/> and a tab
/// control. Keeps the selected index in sync with
/// <see cref="DocumentTabManager.ActiveTab"/>, exposes the display text for
/// each tab (including the dirty marker) and routes close requests through an
/// injected decision callback so no dialog is shown here.
/// </summary>
/// <remarks>
/// This type has no Avalonia dependency and can be exercised in headless
/// tests. The <see cref="DocumentTabStrip"/> control is a thin view over it.
/// </remarks>
public sealed class DocumentTabStripAdapter : IDisposable
{
	private readonly DocumentTabManager manager;
	private readonly Func<DocumentTab, CloseDecision> close_decision;
	private readonly Dictionary<DocumentTab, EventHandler> state_handlers = new ();
	private bool disposed;

	/// <param name="manager">The tab manager to mirror.</param>
	/// <param name="close_decision">
	/// Asked for a <see cref="CloseDecision"/> when the user requests closing a
	/// tab. Returning <see cref="CloseDecision.Cancel"/> keeps the tab open.
	/// </param>
	public DocumentTabStripAdapter (
		DocumentTabManager manager,
		Func<DocumentTab, CloseDecision> close_decision)
	{
		this.manager = manager ?? throw new ArgumentNullException (nameof (manager));
		this.close_decision = close_decision ?? throw new ArgumentNullException (nameof (close_decision));

		manager.Tabs.CollectionChanged += OnTabsChanged;
		manager.ActiveTabChanged += OnActiveTabChanged;
		foreach (DocumentTab tab in manager.Tabs)
			Attach (tab);
		last_selected_index = SelectedIndex;
	}

	/// <summary>The tabs to display, in order.</summary>
	public ObservableCollection<DocumentTab> Tabs => manager.Tabs;

	/// <summary>The manager mirrored by this adapter.</summary>
	public DocumentTabManager Manager => manager;

	/// <summary>
	/// The index of <see cref="DocumentTabManager.ActiveTab"/>, or <c>-1</c>
	/// when no tab is active.
	/// </summary>
	public int SelectedIndex => manager.ActiveTab is null ? -1 : manager.Tabs.IndexOf (manager.ActiveTab);

	/// <summary>Raised when <see cref="SelectedIndex"/> changes.</summary>
	public event EventHandler? SelectedIndexChanged;

	/// <summary>Raised when a tab's title or dirty state changes.</summary>
	public event EventHandler<DocumentTab>? TabStateChanged;

	/// <summary>
	/// Called by the view when the user selects a tab. Returns <c>true</c> when
	/// the selection was applied.
	/// </summary>
	public bool Select (int index)
	{
		if (disposed)
			return false;
		return manager.SwitchTo (index);
	}

	/// <summary>
	/// Called by the view when the user requests closing a tab. Asks the
	/// injected callback for a decision and forwards it to the manager.
	/// Returns <c>true</c> when the tab was removed.
	/// </summary>
	public bool RequestClose (int index)
	{
		if (disposed)
			return false;
		if ((uint) index >= (uint) manager.Tabs.Count)
			return false;

		DocumentTab tab = manager.Tabs[index];
		CloseDecision decision = close_decision (tab);
		return manager.CloseTab (index, decision);
	}

	/// <summary>
	/// The text to show for <paramref name="tab"/>: its title, suffixed with
	/// <c>" *"</c> when it has unsaved changes.
	/// </summary>
	public static string GetDisplayText (DocumentTab tab)
	{
		ArgumentNullException.ThrowIfNull (tab);
		return tab.DisplayTitle;
	}

	private int last_selected_index = -1;

	private void OnTabsChanged (object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems is not null)
			foreach (DocumentTab tab in e.OldItems)
				Detach (tab);
		if (e.NewItems is not null)
			foreach (DocumentTab tab in e.NewItems)
				Attach (tab);

		// A removal can shift the active index without raising ActiveTabChanged.
		RaiseSelectedIndexChanged ();
	}

	private void OnActiveTabChanged (object? sender, EventArgs e)
		=> RaiseSelectedIndexChanged ();

	private void RaiseSelectedIndexChanged ()
	{
		int index = SelectedIndex;
		if (index == last_selected_index)
			return;
		last_selected_index = index;
		SelectedIndexChanged?.Invoke (this, EventArgs.Empty);
	}

	private void Attach (DocumentTab tab)
	{
		if (state_handlers.ContainsKey (tab))
			return;
		EventHandler handler = (_, _) => TabStateChanged?.Invoke (this, tab);
		state_handlers[tab] = handler;
		tab.StateChanged += handler;
	}

	private void Detach (DocumentTab tab)
	{
		if (!state_handlers.Remove (tab, out EventHandler? handler))
			return;
		tab.StateChanged -= handler;
	}

	/// <inheritdoc />
	public void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		manager.Tabs.CollectionChanged -= OnTabsChanged;
		manager.ActiveTabChanged -= OnActiveTabChanged;
		foreach (DocumentTab tab in state_handlers.Keys)
			tab.StateChanged -= state_handlers[tab];
		state_handlers.Clear ();
		SelectedIndexChanged = null;
		TabStateChanged = null;
	}
}

/// <summary>
/// Avalonia view for the open document tabs (multi-document plan, Slice 3).
/// Renders a <see cref="TabControl"/> bound to a
/// <see cref="DocumentTabManager"/> and keeps the selected tab in sync with
/// <see cref="DocumentTabManager.ActiveTab"/>.
/// </summary>
/// <remarks>
/// The control never shows dialogs. Closing a tab is routed through the
/// injected <see cref="Func{T, TResult}"/> so the host window can prompt the
/// user and return a <see cref="CloseDecision"/>.
/// </remarks>
public sealed class DocumentTabStrip : UserControl
{
	private readonly TabControl tab_control;
	private DocumentTabStripAdapter? adapter;
	private Func<DocumentTab, Task<CloseDecision>>? async_close_decision;
	private readonly HashSet<DocumentTab> closing_tabs = new ();
	private DocumentTab? dragging_tab;

	/// <summary>Creates an empty strip; call <see cref="Attach"/> to bind it.</summary>
	public DocumentTabStrip ()
	{
		tab_control = new TabControl {
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};
		tab_control.SelectionChanged += OnSelectionChanged;
		Content = tab_control;
	}

	/// <summary>The adapter currently driving the strip, if any.</summary>
	public DocumentTabStripAdapter? Adapter => adapter;

	/// <summary>
	/// Binds the strip to <paramref name="manager"/>. <paramref name="close_decision"/>
	/// is invoked when the user closes a tab; it must not be null.
	/// </summary>
	public void Attach (
		DocumentTabManager manager,
		Func<DocumentTab, CloseDecision> close_decision)
	{
		ArgumentNullException.ThrowIfNull (manager);
		ArgumentNullException.ThrowIfNull (close_decision);

		Detach ();
		async_close_decision = null;
		adapter = new DocumentTabStripAdapter (manager, close_decision);
		adapter.SelectedIndexChanged += OnAdapterSelectedIndexChanged;
		adapter.TabStateChanged += OnAdapterTabStateChanged;

		// The template must be in place before the items are materialized so
		// every container is built with the DisplayTitle binding already set.
		tab_control.ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<DocumentTab> (
			(_, _) => BuildHeader ());
		// The strip is a header-only control: the host renders the active
		// canvas separately, so the TabControl content area must stay empty.
		// Without a hidden template it would display DocumentTab.ToString().
		tab_control.ContentTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<DocumentTab> (
			(_, _) => new Border { IsVisible = false, Height = 0 });
		tab_control.ItemsSource = adapter.Tabs;
		SyncSelection ();
	}

	/// <summary>Binds the strip with an asynchronous close prompt.</summary>
	public void AttachAsync (
		DocumentTabManager manager,
		Func<DocumentTab, Task<CloseDecision>> close_decision)
	{
		ArgumentNullException.ThrowIfNull (manager);
		ArgumentNullException.ThrowIfNull (close_decision);
		async_close_decision = close_decision;
		Attach (manager, _ => CloseDecision.Cancel);
	}

	/// <summary>Unbinds the strip from its manager.</summary>
	public void Detach ()
	{
		if (adapter is null)
			return;
		adapter.SelectedIndexChanged -= OnAdapterSelectedIndexChanged;
		adapter.TabStateChanged -= OnAdapterTabStateChanged;
		adapter.Dispose ();
		adapter = null;
		async_close_decision = null;
		closing_tabs.Clear ();
		tab_control.ItemsSource = null;
	}

	private Control BuildHeader ()
	{
		var thumbnail = new Image {
			Width = 42,
			Height = 26,
			Stretch = Stretch.Uniform,
			Margin = new Thickness (0, 0, 6, 0)
		};
		DocumentTab? subscribed_tab = null;
		void RefreshThumbnail ()
		{
			if (subscribed_tab is not null)
				thumbnail.Source = subscribed_tab.Canvas.CreateThumbnail (64, 40);
		}
		void UnsubscribeThumbnail ()
		{
			if (subscribed_tab is null)
				return;
			subscribed_tab.Canvas.HistoryChanged -= OnCanvasChanged;
			subscribed_tab.Canvas.LayersChanged -= OnCanvasLayersChanged;
			subscribed_tab = null;
		}
		void OnCanvasChanged (object? sender, EventArgs e) => RefreshThumbnail ();
		void OnCanvasLayersChanged (object? sender, EventArgs e) => RefreshThumbnail ();
		var close = new Button {
			Content = "×",
			Padding = new Thickness (4, 0),
			Margin = new Thickness (6, 0, 0, 0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness (0),
			VerticalAlignment = VerticalAlignment.Center,
			Focusable = false,
		};
		close.Bind (Button.TagProperty, new global::Avalonia.Data.Binding ());
		close.Click += OnCloseClicked;
		// Detach the handler when the header is recycled so a stale button
		// cannot keep the strip (and its adapter) alive.
		close.DetachedFromVisualTree += (_, _) => close.Click -= OnCloseClicked;

		var panel = new StackPanel { Orientation = Orientation.Horizontal };
		panel.DataContextChanged += (_, _) => {
			UnsubscribeThumbnail ();
			if (panel.DataContext is DocumentTab tab) {
				subscribed_tab = tab;
				subscribed_tab.Canvas.HistoryChanged += OnCanvasChanged;
				subscribed_tab.Canvas.LayersChanged += OnCanvasLayersChanged;
				RefreshThumbnail ();
			}
		};
		panel.DetachedFromVisualTree += (_, _) => UnsubscribeThumbnail ();
		panel.PointerPressed += OnTabPointerPressed;
		panel.PointerReleased += OnTabPointerReleased;
		panel.Children.Add (thumbnail);
		panel.Children.Add (close);
		return panel;
	}

	private void OnTabPointerPressed (object? sender, PointerPressedEventArgs e)
	{
		if (e.Source is Button)
			return;
		dragging_tab = (sender as Control)?.DataContext as DocumentTab;
	}

	private void OnTabPointerReleased (object? sender, PointerReleasedEventArgs e)
	{
		DocumentTab? source = dragging_tab;
		dragging_tab = null;
		if (source is null || adapter is null)
			return;
		DocumentTab? target = FindTabDataContext (e.Source as Visual);
		if (target is null || ReferenceEquals (source, target))
			return;
		int from = adapter.Tabs.IndexOf (source);
		int to = adapter.Tabs.IndexOf (target);
		if (from >= 0 && to >= 0)
			adapter.Manager.MoveTab (from, to);
	}

	private static DocumentTab? FindTabDataContext (Visual? visual)
	{
		while (visual is not null) {
			if (visual is TabItem { DataContext: DocumentTab tab })
				return tab;
			visual = visual.GetVisualParent();
		}
		return null;
	}

	private async void OnCloseClicked (object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		DocumentTabStripAdapter? current = adapter;
		if (sender is not Button button || current is null || button.Tag is not DocumentTab tab)
			return;
		e.Handled = true;
		if (!closing_tabs.Add (tab))
			return;
		try {
			if (async_close_decision is not null) {
				CloseDecision decision = await async_close_decision (tab);
				// The adapter may have been detached while the prompt was open;
				// re-resolve the index against the live collection before closing.
				if (!ReferenceEquals (adapter, current))
					return;
				int index = current.Tabs.IndexOf (tab);
				if (index >= 0)
					current.Manager.CloseTab (index, decision);
			} else {
				int index = current.Tabs.IndexOf (tab);
				if (index >= 0)
					current.RequestClose (index);
			}
		} finally {
			closing_tabs.Remove (tab);
		}
	}

	private void OnSelectionChanged (object? sender, SelectionChangedEventArgs e)
	{
		if (adapter is null)
			return;
		int index = tab_control.SelectedIndex;
		if (index != adapter.SelectedIndex)
			adapter.Select (index);
	}

	private void OnAdapterSelectedIndexChanged (object? sender, EventArgs e)
		=> SyncSelection ();

	private void OnAdapterTabStateChanged (object? sender, DocumentTab tab)
	{
		// The header binds to DocumentTab.DisplayTitle, so the dirty marker
		// updates through INotifyPropertyChanged without touching the template.
	}

	private void SyncSelection ()
	{
		if (adapter is null)
			return;
		int index = adapter.SelectedIndex;
		if (tab_control.SelectedIndex != index)
			tab_control.SelectedIndex = index;
	}
}
