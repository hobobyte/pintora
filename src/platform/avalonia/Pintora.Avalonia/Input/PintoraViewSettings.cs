using System;
using Pinta.Core;

namespace Pintora.Avalonia.Input;

public sealed class PintoraViewSettings
{
	public const int DefaultGridSize = 32;
	public const int MinGridSize = 2;
	public const int MaxGridSize = 512;
	private readonly ISettingsService? settings;

	public PintoraViewSettings (ISettingsService? settings = null)
	{
		this.settings = settings;
		Load ();
	}

	public bool ShowGrid { get; set; }
	public int GridSize { get; set; } = DefaultGridSize;
	public bool ShowRulers { get; set; }
	public bool ShowToolbar { get; set; } = true;
	public bool ShowToolbox { get; set; } = true;
	public bool ShowStatusBar { get; set; } = true;
	public bool ShowToolWindows { get; set; } = true;
	public bool ShowColors { get; set; } = true;
	public bool ShowLayers { get; set; } = true;
	public bool ShowImageTabs { get; set; } = true;

	public void Save ()
	{
		if (settings is null) return;
		settings.PutSetting ("Pintora.View.ShowGrid", ShowGrid);
		settings.PutSetting ("Pintora.View.GridSize", Math.Clamp (GridSize, MinGridSize, MaxGridSize));
		settings.PutSetting ("Pintora.View.ShowRulers", ShowRulers);
		settings.PutSetting ("Pintora.View.ShowToolbar", ShowToolbar);
		settings.PutSetting ("Pintora.View.ShowToolbox", ShowToolbox);
		settings.PutSetting ("Pintora.View.ShowStatusBar", ShowStatusBar);
		settings.PutSetting ("Pintora.View.ShowToolWindows", ShowToolWindows);
		settings.PutSetting ("Pintora.View.ShowColors", ShowColors);
		settings.PutSetting ("Pintora.View.ShowLayers", ShowLayers);
		settings.PutSetting ("Pintora.View.ShowImageTabs", ShowImageTabs);
	}

	public void Load ()
	{
		if (settings is null) return;
		ShowGrid = settings.GetSetting ("Pintora.View.ShowGrid", ShowGrid);
		GridSize = Math.Clamp (settings.GetSetting ("Pintora.View.GridSize", GridSize), MinGridSize, MaxGridSize);
		ShowRulers = settings.GetSetting ("Pintora.View.ShowRulers", ShowRulers);
		ShowToolbar = settings.GetSetting ("Pintora.View.ShowToolbar", ShowToolbar);
		ShowToolbox = settings.GetSetting ("Pintora.View.ShowToolbox", ShowToolbox);
		ShowStatusBar = settings.GetSetting ("Pintora.View.ShowStatusBar", ShowStatusBar);
		ShowToolWindows = settings.GetSetting ("Pintora.View.ShowToolWindows", ShowToolWindows);
		ShowColors = settings.GetSetting ("Pintora.View.ShowColors", ShowColors);
		ShowLayers = settings.GetSetting ("Pintora.View.ShowLayers", ShowLayers);
		ShowImageTabs = settings.GetSetting ("Pintora.View.ShowImageTabs", ShowImageTabs);
	}
}
