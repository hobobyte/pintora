using System;
using System.IO;
using Gio;
using Pinta.Core;
using GioFile = Gio.File;

namespace Pinta.Gui.Widgets;

/// <summary>GTK/Gio implementation of the neutral file reference contract.</summary>
public sealed class GioFileReference : IFileReference
{
	private readonly GioFile file;

	public GioFileReference (GioFile file)
		=> this.file = file ?? throw new ArgumentNullException (nameof (file));

	public string DisplayName => file.GetDisplayName ();

	public Stream OpenRead () => new GioStream (file.Read (null));

	public Stream OpenWrite () => new GioStream (file.Replace ());
}
