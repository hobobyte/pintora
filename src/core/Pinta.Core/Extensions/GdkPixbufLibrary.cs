namespace Pinta.Core;

/// <summary>
/// Registers the GdkPixbuf native library for Core's pixbuf interop (DrawPixbuf/ToImageSurface).
/// The GTK app registers the same names through GdkPixbufExtensions; this guard makes the
/// registration safe to request from neutral code and tests as well.
/// </summary>
public static class GdkPixbufLibrary
{
	private const string LibraryName = "GdkPixbuf";
	private static readonly object sync = new ();
	private static bool registered;

	public static void EnsureRegistered ()
	{
		lock (sync) {
			if (registered) return;
			NativeImportResolver.RegisterLibrary (LibraryName,
				windowsLibraryName: "libgdk_pixbuf-2.0-0.dll",
				linuxLibraryName: "libgdk_pixbuf-2.0.so.0",
				osxLibraryName: "libgdk_pixbuf-2.0.0.dylib",
				targetAssembly: typeof (GdkPixbuf.Pixbuf).Assembly);
			registered = true;
		}
	}
}
