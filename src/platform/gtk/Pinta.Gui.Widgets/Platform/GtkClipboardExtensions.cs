using System;
using System.Threading.Tasks;
using Gdk;

namespace Pinta.Gui.Widgets;

public static class GtkClipboardExtensions
{
	public static Task<Texture?> ReadTextureAsync (this Clipboard clipboard)
	{
		TaskCompletionSource<Texture?> tcs = new ();
		Gdk.Internal.Clipboard.ReadTextureAsync (clipboard.Handle.DangerousGetHandle (), IntPtr.Zero,
			new Gio.Internal.AsyncReadyCallbackAsyncHandler ((_, args, _) => {
				IntPtr result = Gdk.Internal.Clipboard.ReadTextureFinish (clipboard.Handle.DangerousGetHandle (), args.Handle.DangerousGetHandle (), out var error);
				Texture? texture = (Texture?) GObject.Internal.InstanceWrapper.WrapNullableHandle<Texture> (result, ownedRef: true);
				if (!error.IsInvalid) texture = null;
				tcs.SetResult (texture);
			}).NativeCallback, IntPtr.Zero);
		return tcs.Task;
	}

	public static Task<GObject.Value> ReadValueAsync (this Clipboard clipboard, GObject.Type type, int io_priority)
	{
		TaskCompletionSource<GObject.Value> tcs = new ();
		Gdk.Internal.Clipboard.ReadValueAsync (clipboard.Handle.DangerousGetHandle (), type, io_priority, IntPtr.Zero,
			new Gio.Internal.AsyncReadyCallbackAsyncHandler ((_, args, _) => {
				GObject.Internal.ValueUnownedHandle result = Gdk.Internal.Clipboard.ReadValueFinish (clipboard.Handle.DangerousGetHandle (), args.Handle.DangerousGetHandle (), out var error);
				if (!error.IsInvalid) tcs.SetException (new GLib.GException (error));
				else tcs.SetResult (new GObject.Value (result.OwnedCopy ()));
			}).NativeCallback, IntPtr.Zero);
		return tcs.Task;
	}

	public static Clipboard GetDefaultClipboard () => Display.GetDefault ()!.GetClipboard ();
}
