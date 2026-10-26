using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pintora.Avalonia.Document;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class DocumentTabStripAdapterTests
{
	private static DocumentTabManager CreateManager (Func<DocumentTab, bool>? save = null)
		=> new (save ?? (_ => true));

	[Test]
	public void SelectedIndexTracksActiveTab ()
	{
		DocumentTabManager manager = CreateManager ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);

		Assert.That (adapter.SelectedIndex, Is.EqualTo (-1));

		manager.AddNewDocument ();
		manager.AddNewDocument ();
		Assert.That (adapter.SelectedIndex, Is.EqualTo (1));

		manager.SwitchTo (0);
		Assert.That (adapter.SelectedIndex, Is.EqualTo (0));
	}

	[Test]
	public void SelectForwardsToManager ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = manager.AddNewDocument ();
		manager.AddNewDocument ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);

		Assert.That (adapter.Select (0), Is.True);
		Assert.That (manager.ActiveTab, Is.SameAs (first));
		Assert.That (adapter.Select (5), Is.False);
	}

	[Test]
	public void SelectedIndexChangedRaisedOnSwitchAndAdd ()
	{
		DocumentTabManager manager = CreateManager ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);
		int changes = 0;
		adapter.SelectedIndexChanged += (_, _) => changes++;

		manager.AddNewDocument ();
		manager.AddNewDocument ();
		manager.SwitchTo (0);

		Assert.That (changes, Is.GreaterThanOrEqualTo (3));
	}

	[Test]
	public void RequestCloseUsesInjectedDecision ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();
		DocumentTab? asked = null;
		using var adapter = new DocumentTabStripAdapter (manager, t => { asked = t; return CloseDecision.Discard; });

		Assert.That (adapter.RequestClose (0), Is.True);
		Assert.That (asked, Is.SameAs (tab));
		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public void RequestCloseCancelKeepsTab ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Cancel);

		Assert.That (adapter.RequestClose (0), Is.False);
		Assert.That (manager.Tabs, Has.Count.EqualTo (1));
	}

	[Test]
	public void RequestCloseOutOfRangeReturnsFalse ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);

		Assert.That (adapter.RequestClose (-1), Is.False);
		Assert.That (adapter.RequestClose (1), Is.False);
	}

	[Test]
	public void GetDisplayTextMarksDirtyTabs ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();

		Assert.That (DocumentTabStripAdapter.GetDisplayText (tab), Is.EqualTo ("Untitled"));

		tab.Canvas.AddLayer ();
		Assert.That (DocumentTabStripAdapter.GetDisplayText (tab), Is.EqualTo ("Untitled *"));
	}

	[Test]
	public void GetDisplayTextMatchesDisplayTitle ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();

		Assert.That (DocumentTabStripAdapter.GetDisplayText (tab), Is.EqualTo (tab.DisplayTitle));

		tab.Canvas.AddLayer ();
		Assert.That (DocumentTabStripAdapter.GetDisplayText (tab), Is.EqualTo (tab.DisplayTitle));
	}

	[Test]
	public void TabStateChangedRaisedForDirtyAndTitleChanges ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);
		var changed = new List<DocumentTab> ();
		adapter.TabStateChanged += (_, t) => changed.Add (t);

		tab.Canvas.AddLayer ();
		tab.Session.MarkSaved ("/tmp/artwork.png");

		Assert.That (changed, Is.EqualTo (new[] { tab, tab }));
	}

	[Test]
	public void ClosedTabStopsRaisingStateChanges ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);
		int changes = 0;
		adapter.TabStateChanged += (_, _) => changes++;

		manager.CloseTab (0, CloseDecision.Discard);
		tab.Session.MarkDirty ();

		Assert.That (changes, Is.Zero);
	}

	[Test]
	public void DisposeUnsubscribesFromManager ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);
		int changes = 0;
		adapter.SelectedIndexChanged += (_, _) => changes++;
		adapter.Dispose ();

		manager.AddNewDocument ();
		tab.Session.MarkDirty ();

		Assert.That (changes, Is.Zero);
		Assert.That (adapter.Select (0), Is.False);
		Assert.That (adapter.RequestClose (0), Is.False);
	}
}
