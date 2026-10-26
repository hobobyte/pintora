using System.IO;
using Gio;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

/// <summary>GTK/Gio implementation of the neutral file reference contract.</summary>
public sealed class GioFileReference : IFileReference
{
	private readonly File file;

	public GioFileReference (File file)
		=> this.file = file ?? throw new ArgumentNullException (nameof (file));

	public string DisplayName => file.GetDisplayName ();

	public Stream OpenRead () => new GioStream (file.Read (null));

	public Stream OpenWrite () => new GioStream (file.Replace ());
}
