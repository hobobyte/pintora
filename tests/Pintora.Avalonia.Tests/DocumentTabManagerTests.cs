using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Document;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class DocumentTabManagerTests
{
	private static DocumentTabManager CreateManager (Func<DocumentTab, bool>? save = null)
		=> new (save ?? (_ => true));

	[Test]
	public void NewManagerHasNoTabs ()
	{
		DocumentTabManager manager = CreateManager ();

		Assert.That (manager.Tabs, Is.Empty);
		Assert.That (manager.ActiveTab, Is.Null);
	}

	[Test]
	public void AddNewDocumentCreatesActiveCleanTab ()
	{
		DocumentTabManager manager = CreateManager ();

		DocumentTab tab = manager.AddNewDocument (32, 24);

		Assert.That (manager.Tabs, Has.Count.EqualTo (1));
		Assert.That (manager.ActiveTab, Is.SameAs (tab));
		Assert.That (tab.Canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (32, 24)));
		Assert.That (tab.Session, Is.SameAs (tab.Canvas.DocumentSession));
		Assert.That (tab.IsDirty, Is.False);
		Assert.That (tab.Title, Is.EqualTo ("Untitled"));
	}

	[Test]
	public void AddNewDocumentRejectsInvalidSize ()
	{
		DocumentTabManager manager = CreateManager ();

		Assert.That (() => manager.AddNewDocument (0, 10), Throws.TypeOf<ArgumentOutOfRangeException> ());
		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public void AddExistingDocumentRejectsEmptyPath ()
	{
		DocumentTabManager manager = CreateManager ();

		Assert.That (() => manager.AddExistingDocument (""), Throws.TypeOf<ArgumentException> ());
		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public void TabsAreIsolated ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = manager.AddNewDocument (16, 16);
		DocumentTab second = manager.AddNewDocument (16, 16);

		first.Canvas.AddLayer ();
		first.Canvas.PaintCanvasPoint (new PointD (4, 4), ColorBgra.Red);
		first.Canvas.BeginStroke ();
		first.Canvas.PaintCanvasPoint (new PointD (5, 5), ColorBgra.Blue);
		first.Canvas.CommitStroke ();

		Assert.That (first.Canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (second.Canvas.Layers, Is.Empty);
		Assert.That (first.Canvas.CanUndoStroke, Is.True);
		Assert.That (second.Canvas.CanUndoStroke, Is.False);
		Assert.That (first.IsDirty, Is.True);
		Assert.That (second.IsDirty, Is.False);
		Assert.That (first.Canvas, Is.Not.SameAs (second.Canvas));
		Assert.That (first.Session, Is.Not.SameAs (second.Session));
	}

	[Test]
	public void SwitchToSelectsTabAndRaisesEvent ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = manager.AddNewDocument ();
		DocumentTab second = manager.AddNewDocument ();
		var changes = new List<DocumentTab?> ();
		manager.ActiveTabChanged += (_, _) => changes.Add (manager.ActiveTab);

		Assert.That (manager.SwitchTo (0), Is.True);
		Assert.That (manager.ActiveTab, Is.SameAs (first));

		Assert.That (manager.SwitchTo (1), Is.True);
		Assert.That (manager.ActiveTab, Is.SameAs (second));

		Assert.That (changes, Is.EqualTo (new[] { first, second }));
	}

	[Test]
	public void SwitchToSameTabDoesNotRaiseEvent ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument ();
		int changes = 0;
		manager.ActiveTabChanged += (_, _) => changes++;

		Assert.That (manager.SwitchTo (0), Is.True);

		Assert.That (changes, Is.Zero);
	}

	[Test]
	public void SwitchToOutOfRangeReturnsFalse ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument ();

		Assert.That (manager.SwitchTo (-1), Is.False);
		Assert.That (manager.SwitchTo (1), Is.False);
	}

	[Test]
	public void CleanCloseRemovesTabAndSelectsNeighbor ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = manager.AddNewDocument ();
		DocumentTab second = manager.AddNewDocument ();
		DocumentTab third = manager.AddNewDocument ();
		manager.SwitchTo (1);

		Assert.That (manager.CloseTab (1, CloseDecision.Discard), Is.True);

		Assert.That (manager.Tabs, Is.EqualTo (new[] { first, third }));
		Assert.That (manager.ActiveTab, Is.SameAs (third));
		Assert.That (second.Session, Is.Not.Null);
	}

	[Test]
	public void ClosingLastTabSelectsPreviousNeighbor ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = manager.AddNewDocument ();
		manager.AddNewDocument ();
		manager.SwitchTo (1);

		Assert.That (manager.CloseTab (1, CloseDecision.Discard), Is.True);

		Assert.That (manager.ActiveTab, Is.SameAs (first));
	}

	[Test]
	public void ClosingOnlyTabLeavesNoActiveTab ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument ();

		Assert.That (manager.CloseTab (0, CloseDecision.Discard), Is.True);

		Assert.That (manager.Tabs, Is.Empty);
		Assert.That (manager.ActiveTab, Is.Null);
	}

	[Test]
	public void ClosingInactiveTabKeepsActiveTab ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument ();
		DocumentTab active = manager.AddNewDocument ();

		Assert.That (manager.CloseTab (0, CloseDecision.Discard), Is.True);

		Assert.That (manager.ActiveTab, Is.SameAs (active));
	}

	[Test]
	public void DirtySaveInvokesDelegateAndClosesOnSuccess ()
	{
		DocumentTab? saved = null;
		DocumentTabManager manager = CreateManager (tab => { saved = tab; return true; });
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();

		Assert.That (manager.CloseTab (0, CloseDecision.Save), Is.True);

		Assert.That (saved, Is.SameAs (tab));
		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public void DirtyDiscardClosesWithoutSaving ()
	{
		int saves = 0;
		DocumentTabManager manager = CreateManager (_ => { saves++; return true; });
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();

		Assert.That (manager.CloseTab (0, CloseDecision.Discard), Is.True);

		Assert.That (saves, Is.Zero);
		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public void DirtyCancelKeepsTabOpen ()
	{
		int saves = 0;
		DocumentTabManager manager = CreateManager (_ => { saves++; return true; });
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();

		Assert.That (manager.CloseTab (0, CloseDecision.Cancel), Is.False);

		Assert.That (saves, Is.Zero);
		Assert.That (manager.Tabs, Has.Count.EqualTo (1));
		Assert.That (manager.ActiveTab, Is.SameAs (tab));
		Assert.That (tab.IsDirty, Is.True);
	}

	[Test]
	public void FailedSaveKeepsTabOpen ()
	{
		DocumentTabManager manager = CreateManager (_ => false);
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();

		Assert.That (manager.CloseTab (0, CloseDecision.Save), Is.False);

		Assert.That (manager.Tabs, Has.Count.EqualTo (1));
		Assert.That (manager.ActiveTab, Is.SameAs (tab));
		Assert.That (tab.IsDirty, Is.True);
	}

	[Test]
	public void ThrowingSaveKeepsTabOpen ()
	{
		DocumentTabManager manager = CreateManager (_ => throw new InvalidOperationException ("disk full"));
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();

		Assert.That (manager.CloseTab (0, CloseDecision.Save), Is.False);

		Assert.That (manager.Tabs, Has.Count.EqualTo (1));
		Assert.That (manager.ActiveTab, Is.SameAs (tab));
	}

	[Test]
	public void CleanTabDoesNotInvokeSaveDelegate ()
	{
		int saves = 0;
		DocumentTabManager manager = CreateManager (_ => { saves++; return true; });
		manager.AddNewDocument ();

		Assert.That (manager.CloseTab (0, CloseDecision.Save), Is.True);

		Assert.That (saves, Is.Zero);
	}

	[Test]
	public void CloseTabOutOfRangeReturnsFalse ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument ();

		Assert.That (manager.CloseTab (-1, CloseDecision.Discard), Is.False);
		Assert.That (manager.CloseTab (1, CloseDecision.Discard), Is.False);
	}

	[Test]
	public void CloseActiveTabUsesActiveIndex ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument ();
		DocumentTab active = manager.AddNewDocument ();

		Assert.That (manager.CloseActiveTab (CloseDecision.Discard), Is.True);

		Assert.That (manager.Tabs, Has.Count.EqualTo (1));
		Assert.That (manager.Tabs, Does.Not.Contain (active));
	}

	[Test]
	public void CloseActiveTabWithoutTabsReturnsFalse ()
	{
		DocumentTabManager manager = CreateManager ();

		Assert.That (manager.CloseActiveTab (CloseDecision.Discard), Is.False);
	}

	[Test]
	public void TabTitleTracksSessionState ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		int changes = 0;
		tab.StateChanged += (_, _) => changes++;

		tab.Session.MarkSaved ("/tmp/artwork.png");

		Assert.That (tab.Title, Is.EqualTo ("artwork.png"));
		Assert.That (changes, Is.EqualTo (1));
	}

	[Test]
	public void DisplayTitleTracksDirtyStateAndRaisesPropertyChanged ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		var changed = new List<string?> ();
		tab.PropertyChanged += (_, e) => changed.Add (e.PropertyName);

		Assert.That (tab.DisplayTitle, Is.EqualTo ("Untitled"));

		tab.Canvas.AddLayer ();
		Assert.That (tab.DisplayTitle, Is.EqualTo ("Untitled *"));

		tab.Session.MarkSaved ("/tmp/artwork.png");
		Assert.That (tab.DisplayTitle, Is.EqualTo ("artwork.png"));

		Assert.That (changed, Is.EqualTo (new[] {
			nameof (DocumentTab.DisplayTitle), nameof (DocumentTab.Title), nameof (DocumentTab.IsDirty),
			nameof (DocumentTab.DisplayTitle), nameof (DocumentTab.Title), nameof (DocumentTab.IsDirty)
		}));
	}

	[Test]
	public void ClosedTabStopsForwardingSessionState ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument ();
		int changes = 0;
		tab.StateChanged += (_, _) => changes++;

		manager.CloseTab (0, CloseDecision.Discard);
		tab.Session.MarkDirty ();

		Assert.That (changes, Is.Zero);
	}

	[Test]
	public void TabsCollectionRaisesNotificationsForTabControl ()
	{
		DocumentTabManager manager = CreateManager ();
		var actions = new List<System.Collections.Specialized.NotifyCollectionChangedAction> ();
		manager.Tabs.CollectionChanged += (_, e) => actions.Add (e.Action);

		manager.AddNewDocument ();
		manager.CloseTab (0, CloseDecision.Discard);

		Assert.That (actions, Is.EqualTo (new[] {
			System.Collections.Specialized.NotifyCollectionChangedAction.Add,
			System.Collections.Specialized.NotifyCollectionChangedAction.Remove,
		}));
	}
}
