using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Input;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class PintoraViewSettingsTests
{
	[Test]
	public void DefaultsAreStable ()
	{
		PintoraViewSettings settings = new ();

		Assert.That (settings.ShowGrid, Is.False);
		Assert.That (settings.GridSize, Is.EqualTo (PintoraViewSettings.DefaultGridSize));
		Assert.That (settings.ShowRulers, Is.False);
		Assert.That (settings.ShowToolbar, Is.True);
		Assert.That (settings.ShowToolbox, Is.True);
		Assert.That (settings.ShowStatusBar, Is.True);
		Assert.That (settings.ShowToolWindows, Is.True);
		Assert.That (settings.ShowImageTabs, Is.True);
	}

	[Test]
	public void SaveAndLoadRoundTripsThroughSettingsService ()
	{
		var stored = new MemorySettings ();
		PintoraViewSettings first = new (stored) {
			ShowGrid = true,
			GridSize = 64,
			ShowRulers = true,
			ShowToolbar = false,
			ShowToolbox = false,
			ShowStatusBar = false,
			ShowToolWindows = false,
			ShowImageTabs = false
		};
		first.Save ();

		PintoraViewSettings reloaded = new (stored);

		Assert.That (reloaded.ShowGrid, Is.True);
		Assert.That (reloaded.GridSize, Is.EqualTo (64));
		Assert.That (reloaded.ShowRulers, Is.True);
		Assert.That (reloaded.ShowToolbar, Is.False);
		Assert.That (reloaded.ShowToolbox, Is.False);
		Assert.That (reloaded.ShowStatusBar, Is.False);
		Assert.That (reloaded.ShowToolWindows, Is.False);
		Assert.That (reloaded.ShowImageTabs, Is.False);
	}

	[Test]
	public void GridSizeIsClampedOnLoad ()
	{
		var stored = new MemorySettings ();
		stored.PutSetting ("Pintora.View.GridSize", 1);

		PintoraViewSettings settings = new (stored);

		Assert.That (settings.GridSize, Is.EqualTo (PintoraViewSettings.MinGridSize));
	}

	[Test]
	public void GridToggleRoundTripsThroughSettingsService ()
	{
		var stored = new MemorySettings ();
		PintoraViewSettings settings = new (stored);

		settings.ShowGrid = !settings.ShowGrid;
		settings.Save ();

		PintoraViewSettings reloaded = new (stored);
		Assert.That (reloaded.ShowGrid, Is.True);
	}

	[Test]
	public void GridSettingsCanBeAppliedToMultipleCanvases ()
	{
		var stored = new MemorySettings ();
		PintoraViewSettings settings = new (stored) { ShowGrid = true, GridSize = 48 };
		settings.Save ();

		PintoraViewSettings reloaded = new (stored);
		CanvasHost first = new (new Pinta.Core.Size (100, 100));
		CanvasHost second = new (new Pinta.Core.Size (100, 100));

		foreach (CanvasHost canvas in new[] { first, second }) {
			canvas.GridSize = reloaded.GridSize;
			canvas.ShowGrid = reloaded.ShowGrid;
		}

		Assert.That (first.ShowGrid, Is.True);
		Assert.That (first.GridSize, Is.EqualTo (48));
		Assert.That (second.ShowGrid, Is.True);
		Assert.That (second.GridSize, Is.EqualTo (48));
	}

	[Test]
	public void TogglingGridPersistsAcrossReload ()
	{
		var stored = new MemorySettings ();
		PintoraViewSettings settings = new (stored);

		settings.ShowGrid = !settings.ShowGrid;
		settings.Save ();
		PintoraViewSettings reloaded = new (stored);
		reloaded.ShowGrid = !reloaded.ShowGrid;
		reloaded.Save ();

		Assert.That (new PintoraViewSettings (stored).ShowGrid, Is.False);
	}

	[Test]
	public void SaveWithoutServiceIsNoOp ()
	{
		PintoraViewSettings settings = new () { ShowGrid = true, GridSize = 16 };

		Assert.DoesNotThrow (() => settings.Save ());
		Assert.DoesNotThrow (() => settings.Load ());
		Assert.That (settings.ShowGrid, Is.True);
		Assert.That (settings.GridSize, Is.EqualTo (16));
	}

	private sealed class MemorySettings : ISettingsService
	{
		private readonly Dictionary<string, object> values = new ();

		#pragma warning disable CS0067
		public event EventHandler? SaveSettingsBeforeQuit;
		public event EventHandler<SettingChangedEventArgs>? SettingChanged;
		#pragma warning restore CS0067

		public T GetSetting<T> (string key, T defaultValue)
			=> values.TryGetValue (key, out object? value) && value is T typed ? typed : defaultValue;

		public string GetUserSettingsDirectory () => string.Empty;

		public void PutSetting (string key, object value) => values[key] = value;
	}
}
