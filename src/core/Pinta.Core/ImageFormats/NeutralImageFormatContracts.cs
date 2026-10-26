namespace Pinta.Core;

/// <summary>Image import contract that does not expose Gio.</summary>
public interface IImageFileImporter
{
	Document Import (IFileReference file);
}

/// <summary>Image export contract that does not expose Gio or Gtk.</summary>
public interface IImageFileExporter
{
	void Export (Document document, IFileReference file);
}
