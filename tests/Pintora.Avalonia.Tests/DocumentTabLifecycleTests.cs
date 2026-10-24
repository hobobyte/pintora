using System;
using NUnit.Framework;
using Pintora.Avalonia.Document;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class DocumentTabLifecycleTests
{
	[Test]
	public void ClosingTabReleasesExactlyOnce ()
	{
		int releases = 0;
		var manager = new DocumentTabManager (_ => true, _ => releases++);
		manager.AddNewDocument ();

		Assert.That (manager.CloseTab (0, CloseDecision.Discard), Is.True);
		manager.Dispose ();
		Assert.That (releases, Is.EqualTo (1));
	}

	[Test]
	public void CancelAndFailedSaveKeepTabAndDoNotRelease ()
	{
		int releases = 0;
		var manager = new DocumentTabManager (_ => false, _ => releases++);
		DocumentTab tab = manager.AddNewDocument ();
		tab.Canvas.AddLayer ();

		Assert.That (manager.CloseTab (0, CloseDecision.Cancel), Is.False);
		Assert.That (manager.CloseTab (0, CloseDecision.Save), Is.False);
		Assert.That (manager.Tabs, Has.Count.EqualTo (1));
		Assert.That (releases, Is.Zero);
	}

	[Test]
	public void DisposingManagerReleasesRemainingTabs ()
	{
		int releases = 0;
		var manager = new DocumentTabManager (_ => true, _ => releases++);
		manager.AddNewDocument ();
		manager.AddNewDocument ();

		manager.Dispose ();
		manager.Dispose ();

		Assert.That (releases, Is.EqualTo (2));
		Assert.That (manager.Tabs, Is.Empty);
	}
}
