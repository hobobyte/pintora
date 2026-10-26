namespace Pinta.Core;

/// <summary>GTK/Gio adapters for file operations used by the platform layer.</summary>
public static class GioFileExtensions
{
	public static string GetDisplayName (this Gio.File file)
	{
		Gio.FileInfo info = file.QueryInfo (
			Gio.Constants.FILE_ATTRIBUTE_STANDARD_DISPLAY_NAME,
			Gio.FileQueryInfoFlags.None,
			cancellable: null);
		return info.GetDisplayName ();
	}

	public static string GetSafeDisplayName (this Gio.File file)
	{
		try {
			return file.GetDisplayName ();
		} catch (GLib.GException) {
			return file.GetParent ()?.GetRelativePath (file) ?? string.Empty;
		}
	}

	public static Gio.OutputStream Replace (this Gio.File file)
		=> file.Replace (etag: null, makeBackup: false, flags: Gio.FileCreateFlags.None, cancellable: null);
}
