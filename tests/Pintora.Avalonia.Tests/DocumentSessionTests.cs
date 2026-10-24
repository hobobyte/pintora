using System;
using NUnit.Framework;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class DocumentSessionTests
{
	private static DocumentSession CreateSession (string? path = null)
	{
		var document = new AvaloniaDocumentSession (new Pinta.Core.Size (16, 16), new MemoryImageSurface (16, 16));
		return new DocumentSession (document, path);
	}

	[Test]
	public void NewDocumentStartsCleanAndUntitled ()
	{
		DocumentSession session = CreateSession ();

		Assert.That (session.IsDirty, Is.False);
		Assert.That (session.FilePath, Is.Null);
		Assert.That (session.Title, Is.EqualTo ("Untitled"));
	}

	[Test]
	public void MarkDirtyTransitionsOnceAndRaisesStateChanged ()
	{
		DocumentSession session = CreateSession ();
		int changes = 0;
		session.StateChanged += (_, _) => changes++;

		session.MarkDirty ();
		session.MarkDirty ();

		Assert.That (session.IsDirty, Is.True);
		Assert.That (changes, Is.EqualTo (1));
	}

	[Test]
	public void MarkCleanTransitionsOnceAndRaisesStateChanged ()
	{
		DocumentSession session = CreateSession ();
		session.MarkDirty ();
		int changes = 0;
		session.StateChanged += (_, _) => changes++;

		session.MarkClean ();
		session.MarkClean ();

		Assert.That (session.IsDirty, Is.False);
		Assert.That (changes, Is.EqualTo (1));
	}

	[Test]
	public void MarkSavedUpdatesPathTitleAndClearsDirty ()
	{
		DocumentSession session = CreateSession ();
		session.MarkDirty ();
		int changes = 0;
		session.StateChanged += (_, _) => changes++;

		session.MarkSaved ("/tmp/artwork.png");

		Assert.That (session.IsDirty, Is.False);
		Assert.That (session.FilePath, Is.EqualTo ("/tmp/artwork.png"));
		Assert.That (session.Title, Is.EqualTo ("artwork.png"));
		Assert.That (changes, Is.EqualTo (1));
	}

	[Test]
	public void MarkSavedWithSamePathOnlyRaisesWhenDirtyChanged ()
	{
		DocumentSession session = CreateSession ("/tmp/artwork.png");
		int changes = 0;
		session.StateChanged += (_, _) => changes++;

		session.MarkSaved ("/tmp/artwork.png");
		Assert.That (changes, Is.Zero);

		session.MarkDirty ();
		session.MarkSaved ("/tmp/artwork.png");
		Assert.That (changes, Is.EqualTo (2));
	}

	[Test]
	public void MarkSavedRejectsEmptyPath ()
	{
		DocumentSession session = CreateSession ();

		Assert.That (() => session.MarkSaved (""), Throws.TypeOf<ArgumentException> ());
		Assert.That (session.FilePath, Is.Null);
	}

	[Test]
	public void SessionsAreIsolated ()
	{
		DocumentSession first = CreateSession ();
		DocumentSession second = CreateSession ();

		first.MarkDirty ();
		first.MarkSaved ("/tmp/first.png");

		Assert.That (first.IsDirty, Is.False);
		Assert.That (first.FilePath, Is.EqualTo ("/tmp/first.png"));
		Assert.That (second.IsDirty, Is.False);
		Assert.That (second.FilePath, Is.Null);
		Assert.That (second.Title, Is.EqualTo ("Untitled"));
		Assert.That (first.Document, Is.Not.SameAs (second.Document));
	}
}
