using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Document;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class DocumentTabLifecycleExtraTests
{
	[Test]
	public void DisposeClearsActiveTabBeforeReleasing ()
	{
		var manager = new DocumentTabManager (_ => true);
		manager.AddNewDocument ();
		DocumentTab? observed = manager.ActiveTab;
		manager.ActiveTabChanged += (_, _) => observed = manager.ActiveTab;

		manager.Dispose ();

		Assert.That (observed, Is.Null);
		Assert.That (manager.ActiveTab, Is.Null);
		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public void ReleaseIsCalledOncePerTabEvenAfterDisposeTwice ()
	{
		var released = new List<DocumentTab> ();
		var manager = new DocumentTabManager (_ => true, tab => released.Add (tab));
		DocumentTab first = manager.AddNewDocument ();
		DocumentTab second = manager.AddNewDocument ();

		manager.Dispose ();
		manager.Dispose ();

		Assert.That (released, Is.EquivalentTo (new[] { first, second }));
	}

	[Test]
	public void ClosingTabReleasesOnlyThatTab ()
	{
		var released = new List<DocumentTab> ();
		var manager = new DocumentTabManager (_ => true, tab => released.Add (tab));
		DocumentTab first = manager.AddNewDocument ();
		DocumentTab second = manager.AddNewDocument ();

		Assert.That (manager.CloseTab (0, CloseDecision.Discard), Is.True);

		Assert.That (released, Is.EqualTo (new[] { first }));
		Assert.That (manager.Tabs, Is.EqualTo (new[] { second }));
	}

	[Test]
	public void AdapterDoesNotRaiseSelectedIndexChangedForUnchangedIndex ()
	{
		var manager = new DocumentTabManager (_ => true);
		manager.AddNewDocument ();
		using var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);
		int changes = 0;
		adapter.SelectedIndexChanged += (_, _) => changes++;

		// Adding a tab changes the active index (0 -> 1) once.
		manager.AddNewDocument ();
		Assert.That (changes, Is.EqualTo (1));

		// Switching to the already-active tab must not raise.
		manager.SwitchTo (1);
		Assert.That (changes, Is.EqualTo (1));
	}

	[Test]
	public void AdapterDisposeClearsEventHandlers ()
	{
		var manager = new DocumentTabManager (_ => true);
		manager.AddNewDocument ();
		var adapter = new DocumentTabStripAdapter (manager, _ => CloseDecision.Discard);
		int changes = 0;
		adapter.SelectedIndexChanged += (_, _) => changes++;

		adapter.Dispose ();
		manager.AddNewDocument ();

		Assert.That (changes, Is.Zero);
	}

	[Test]
	public void ActiveCanvasCommandsTargetTheActiveTab ()
	{
		var manager = new DocumentTabManager (_ => true);
		DocumentTab first = manager.AddNewDocument (16, 16);
		DocumentTab second = manager.AddNewDocument (16, 16);

		// Simulate a command routed through ActiveCanvas.
		manager.ActiveTab!.Canvas.AddLayer ();
		Assert.That (second.Canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (first.Canvas.Layers, Is.Empty);

		manager.SwitchTo (0);
		manager.ActiveTab!.Canvas.AddLayer ();
		Assert.That (first.Canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (second.Canvas.Layers, Has.Count.EqualTo (1));
	}

	[Test]
	public void TitleTracksActiveTabAfterSwitch ()
	{
		var manager = new DocumentTabManager (_ => true);
		DocumentTab first = manager.AddNewDocument ();
		DocumentTab second = manager.AddNewDocument ();
		first.Session.MarkSaved ("/tmp/first.png");
		second.Session.MarkSaved ("/tmp/second.png");

		Assert.That (manager.ActiveTab!.Title, Is.EqualTo ("second.png"));
		manager.SwitchTo (0);
		Assert.That (manager.ActiveTab!.Title, Is.EqualTo ("first.png"));
	}

	[Test]
	public void DirtyStateIsPerTabAfterSwitch ()
	{
		var manager = new DocumentTabManager (_ => true);
		DocumentTab first = manager.AddNewDocument ();
		DocumentTab second = manager.AddNewDocument ();
		first.Canvas.AddLayer ();

		Assert.That (first.IsDirty, Is.True);
		Assert.That (second.IsDirty, Is.False);
		manager.SwitchTo (1);
		Assert.That (manager.ActiveTab!.IsDirty, Is.False);
	}

	[Test]
	public void DisplayTitleIsStableAcrossSwitches ()
	{
		var manager = new DocumentTabManager (_ => true);
		DocumentTab first = manager.AddNewDocument ();
		DocumentTab second = manager.AddNewDocument ();
		first.Session.MarkSaved ("/tmp/first.png");
		second.Session.MarkSaved ("/tmp/second.png");

		string firstTitle = first.DisplayTitle;
		string secondTitle = second.DisplayTitle;

		manager.SwitchTo (0);
		Assert.That (first.DisplayTitle, Is.EqualTo (firstTitle));
		Assert.That (second.DisplayTitle, Is.EqualTo (secondTitle));
		manager.SwitchTo (1);
		Assert.That (first.DisplayTitle, Is.EqualTo (firstTitle));
		Assert.That (second.DisplayTitle, Is.EqualTo (secondTitle));
		Assert.That (firstTitle, Is.EqualTo ("first.png"));
		Assert.That (secondTitle, Is.EqualTo ("second.png"));
	}

	[Test]
	public void SwitchingTabsDoesNotMutateTextToolText ()
	{
		var manager = new DocumentTabManager (_ => true);
		DocumentTab first = manager.AddNewDocument (16, 16);
		DocumentTab second = manager.AddNewDocument (16, 16);
		first.Canvas.TextToolText = "First document";
		second.Canvas.TextToolText = "Second document";

		manager.SwitchTo (0);
		manager.SwitchTo (1);
		manager.SwitchTo (0);

		Assert.That (first.Canvas.TextToolText, Is.EqualTo ("First document"));
		Assert.That (second.Canvas.TextToolText, Is.EqualTo ("Second document"));
	}

	[Test]
	public void SwitchingTabsDoesNotReapplyGlobalToolSettings ()
	{
		var manager = new DocumentTabManager (_ => true);
		DocumentTab first = manager.AddNewDocument (16, 16);
		DocumentTab second = manager.AddNewDocument (16, 16);
		first.Canvas.BrushSize = 7;
		second.Canvas.BrushSize = 21;

		manager.SwitchTo (0);
		Assert.That (first.Canvas.BrushSize, Is.EqualTo (7));
		manager.SwitchTo (1);
		Assert.That (second.Canvas.BrushSize, Is.EqualTo (21));
	}

	[Test]
	public void SelectedToolAndTextOptionsPersistPerTabAfterSwitch ()
	{
		var manager = new DocumentTabManager (_ => true);
		DocumentTab first = manager.AddNewDocument (16, 16);
		DocumentTab second = manager.AddNewDocument (16, 16);
		first.Canvas.ActiveTool = CanvasHost.CanvasTool.Text;
		first.Canvas.TextToolText = "First document";
		second.Canvas.ActiveTool = CanvasHost.CanvasTool.Brush;
		second.Canvas.TextToolText = "Second document";

		manager.SwitchTo (0);
		Assert.That (manager.ActiveTab!.Canvas.ActiveTool, Is.EqualTo (CanvasHost.CanvasTool.Text));
		Assert.That (manager.ActiveTab.Canvas.TextToolText, Is.EqualTo ("First document"));
		manager.SwitchTo (1);
		Assert.That (manager.ActiveTab!.Canvas.ActiveTool, Is.EqualTo (CanvasHost.CanvasTool.Brush));
		Assert.That (manager.ActiveTab.Canvas.TextToolText, Is.EqualTo ("Second document"));
	}
}
