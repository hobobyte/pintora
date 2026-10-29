using Pinta.Core;

namespace Pinta.Application;

/// <summary>Explicit application service graph, independent from the static PintaCore facade.</summary>
public sealed class PintaApplication
{
	private PintaApplication (IServiceManager services, IImageFormatProvider imageFormatProvider)
	{
		Services = services;
		ImageFormatProvider = imageFormatProvider;
	}

	public IServiceManager Services { get; }
	public IImageFormatProvider ImageFormatProvider { get; }

	public T GetService<T> () where T : class => Services.GetService<T> ();

	public static PintaApplication Build (IImageFormatProvider imageFormatProvider, bool includeActions = true)
	{
		ArgumentNullException.ThrowIfNull (imageFormatProvider);
		var settings = new SettingsManager ();
		var system = new SystemManager ();
		var chrome = new ChromeManager ();
		var paintBrushes = new PaintBrushManager ();
		var paletteFormats = new PaletteFormatManager ();
		var recentFiles = new RecentFileManager ();
		var imageFormats = new ImageConverterManager (settings, imageFormatProvider);
		var workspace = new WorkspaceManager (system, chrome, imageFormats);
		var tools = new ToolManager (workspace, chrome);
		var palette = new PaletteManager (settings, paletteFormats);
		ActionManager? actions = includeActions ? new ActionManager (chrome, imageFormats, paletteFormats, palette, recentFiles, system, tools, workspace) : null;
		var livePreview = new LivePreviewManager (workspace, tools, system, chrome);
		EffectsManager? effects = actions is null ? null : new EffectsManager (actions, chrome, livePreview);
		var canvasGrid = new CanvasGridManager (workspace, settings);
		var resources = new ResourceManager ();
		var services = new ServiceManager ();
		services.AddService<IResourceService> (resources);
		services.AddService<ISettingsService> (settings);
		services.AddService (settings);
		if (actions is not null)
			services.AddService (actions);
		services.AddService<IWorkspaceService> (workspace);
		services.AddService<IPaintBrushService> (paintBrushes);
		services.AddService<IToolService> (tools);
		services.AddService (imageFormats);
		services.AddService (paletteFormats);
		services.AddService (system);
		services.AddService (recentFiles);
		services.AddService<ILivePreview> (livePreview);
		services.AddService<IPaletteService> (palette);
		services.AddService<IChromeService> (chrome);
		services.AddService<ISystemService> (system);
		if (effects is not null)
			services.AddService (effects);
		services.AddService<ICanvasGridService> (canvasGrid);
		return new PintaApplication (services, imageFormatProvider);
	}
}
