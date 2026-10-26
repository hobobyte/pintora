using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Pintora.Avalonia.Document;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class DocumentCloseCoordinatorTests
{
	private static DocumentTabManager CreateManager ()
		=> new (_ => true);

	private static DocumentCloseCoordinator CreateCoordinator (
		DocumentTabManager manager,
		Func<DocumentTab, CloseDecision> prompt,
		Func<DocumentTab, bool>? save = null)
		=> new (manager, tab => Task.FromResult (prompt (tab)), tab => Task.FromResult ((save ?? (_ => true)) (tab)));

	private static DocumentTab AddDirtyTab (DocumentTabManager manager)
	{
		DocumentTab tab = manager.AddNewDocument (16, 16);
		tab.Canvas.AddLayer ();
		Assert.That (tab.IsDirty, Is.True);
		return tab;
	}

	[Test]
	public async Task SaveAllSavesEveryDirtyTab ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = AddDirtyTab (manager);
		DocumentTab second = AddDirtyTab (manager);
		var saved = new List<DocumentTab> ();
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Save, tab => { saved.Add (tab); return true; });

		Assert.That (await coordinator.SaveAllAsync (), Is.True);

		Assert.That (saved, Is.EqualTo (new[] { first, second }));
		Assert.That (manager.Tabs, Has.Count.EqualTo (2));
	}

	[Test]
	public async Task SaveAllSkipsCleanTabs ()
	{
		DocumentTabManager manager = CreateManager ();
		manager.AddNewDocument (16, 16);
		int saves = 0;
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Save, _ => { saves++; return true; });

		Assert.That (await coordinator.SaveAllAsync (), Is.True);

		Assert.That (saves, Is.Zero);
	}

	[Test]
	public async Task SaveAllStopsOnFailedSaveAndKeepsDirtyTabs ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = AddDirtyTab (manager);
		DocumentTab second = AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Save, tab => !ReferenceEquals (tab, first));

		Assert.That (await coordinator.SaveAllAsync (), Is.False);

		Assert.That (manager.Tabs, Is.EqualTo (new[] { first, second }));
		Assert.That (first.IsDirty, Is.True);
		Assert.That (second.IsDirty, Is.True);
	}

	[Test]
	public async Task SaveAllTreatsThrowingSaveAsFailure ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Save, _ => throw new InvalidOperationException ("disk full"));

		Assert.That (await coordinator.SaveAllAsync (), Is.False);

		Assert.That (manager.Tabs, Is.EqualTo (new[] { tab }));
		Assert.That (tab.IsDirty, Is.True);
	}

	[Test]
	public async Task CloseAllDiscardsDirtyTabsWhenConfirmed ()
	{
		DocumentTabManager manager = CreateManager ();
		AddDirtyTab (manager);
		AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Discard);

		Assert.That (await coordinator.CloseAllAsync (), Is.True);

		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public async Task CloseAllStopsOnCancelAndKeepsRemainingTabs ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab first = AddDirtyTab (manager);
		DocumentTab second = AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, tab => ReferenceEquals (tab, first) ? CloseDecision.Discard : CloseDecision.Cancel);

		Assert.That (await coordinator.CloseAllAsync (), Is.False);

		Assert.That (manager.Tabs, Is.EqualTo (new[] { second }));
		Assert.That (second.IsDirty, Is.True);
	}

	[Test]
	public async Task CloseAllStopsOnFailedSaveAndKeepsDirtyTab ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Save, _ => false);

		Assert.That (await coordinator.CloseAllAsync (), Is.False);

		Assert.That (manager.Tabs, Is.EqualTo (new[] { tab }));
		Assert.That (tab.IsDirty, Is.True);
	}

	[Test]
	public async Task CloseAllSavesDirtyTabsWhenRequested ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Save, _ => true);

		Assert.That (await coordinator.CloseAllAsync (), Is.True);

		Assert.That (manager.Tabs, Is.Empty);
		Assert.That (tab.IsDirty, Is.True, "The coordinator must not mutate dirty state itself.");
	}

	[Test]
	public async Task TryExitClosesAllTabsWhenConfirmed ()
	{
		DocumentTabManager manager = CreateManager ();
		AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Discard);

		Assert.That (await coordinator.TryExitAsync (), Is.True);

		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public async Task TryExitKeepsTabsOnCancel ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Cancel);

		Assert.That (await coordinator.TryExitAsync (), Is.False);

		Assert.That (manager.Tabs, Is.EqualTo (new[] { tab }));
		Assert.That (tab.IsDirty, Is.True);
	}

	[Test]
	public async Task CloseTabDoesNotPromptCleanTab ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = manager.AddNewDocument (16, 16);
		int prompts = 0;
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => { prompts++; return CloseDecision.Cancel; });

		Assert.That (await coordinator.CloseTabAsync (tab), Is.True);

		Assert.That (prompts, Is.Zero);
		Assert.That (manager.Tabs, Is.Empty);
	}

	[Test]
	public async Task CloseTabKeepsDirtyTabOnCancel ()
	{
		DocumentTabManager manager = CreateManager ();
		DocumentTab tab = AddDirtyTab (manager);
		DocumentCloseCoordinator coordinator = CreateCoordinator (manager, _ => CloseDecision.Cancel);

		Assert.That (await coordinator.CloseTabAsync (tab), Is.False);

		Assert.That (manager.Tabs, Is.EqualTo (new[] { tab }));
		Assert.That (tab.IsDirty, Is.True);
	}
}
