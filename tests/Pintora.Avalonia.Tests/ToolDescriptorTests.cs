using System.Linq;
using NUnit.Framework;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class ToolDescriptorTests
{
	[Test]
	public void DescriptorsUseStableIdsAndGroups ()
	{
		ToolDescriptor[] descriptors = typeof (CanvasToolFactory)
			.GetProperty (nameof (CanvasToolFactory.Descriptors))!
			.GetValue (null) as ToolDescriptor[] ?? [];

		Assert.That (descriptors.Length, Is.GreaterThanOrEqualTo (11));
		Assert.That (descriptors.Select (item => item.Id).Distinct ().Count (), Is.EqualTo (descriptors.Length));
		Assert.That (descriptors.Single (item => item.Tool == CanvasHost.CanvasTool.Blur).Group, Is.EqualTo ("Effects"));
		Assert.That (descriptors.Single (item => item.Tool == CanvasHost.CanvasTool.Magnify).Group, Is.EqualTo ("Effects"));
		Assert.That (descriptors.Single (item => item.Tool == CanvasHost.CanvasTool.Spotlight).Group, Is.EqualTo ("Effects"));
	}

	[Test]
	public void EffectOptionsAreImmutableAndTyped ()
	{
		IToolOptions options = new EffectToolOptions { Strength = 0.5, PixelSize = 12 };

		Assert.That (options, Is.TypeOf<EffectToolOptions> ());
		Assert.That (((EffectToolOptions) options).Strength, Is.EqualTo (0.5));
		Assert.That (((EffectToolOptions) options).PixelSize, Is.EqualTo (12));
	}

	[Test]
	public void TextToolUsesAUnifiedOptionsObject ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		TextToolOptions options = canvas.GetTextToolOptions ();

		Assert.That (options.Text, Is.Empty);
		Assert.That (options.FontFamily, Is.EqualTo ("Inter"));
		Assert.That (options.FontSize, Is.EqualTo (16));
		canvas.TextToolBold = true;
		Assert.That (options.Bold, Is.True);
	}

	[Test]
	public void TextOptionChangesNotifyTheHost ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		int notifications = 0;
		canvas.ToolOptionsChanged += (_, _) => notifications++;

		canvas.TextToolFontSize = 24;
		canvas.TextToolUnderline = true;

		Assert.That (notifications, Is.EqualTo (2));
	}

	[Test]
	public void BrushToolsShareAUnifiedOptionsObject ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		BrushToolOptions options = canvas.GetBrushToolOptions ();

		canvas.BrushSize = 9;
		canvas.BrushOpacity = 0.5;
		canvas.BrushHardness = 0.25;

		Assert.That (options.Size, Is.EqualTo (9));
		Assert.That (options.Opacity, Is.EqualTo (0.5));
		Assert.That (options.Hardness, Is.EqualTo (0.25));
	}

	[Test]
	public void ShapeToolUsesAUnifiedOptionsObject ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		ShapeToolOptions options = canvas.GetShapeToolOptions ();
		int notifications = 0;
		canvas.ToolOptionsChanged += (_, _) => notifications++;

		canvas.SelectedShape = CanvasHost.ShapeKind.Ellipse;
		canvas.ShapeOutlineWidth = 5;
		canvas.ShapeAntialias = false;

		Assert.That (options.Kind, Is.EqualTo (CanvasHost.ShapeKind.Ellipse));
		Assert.That (options.OutlineWidth, Is.EqualTo (5));
		Assert.That (options.Antialias, Is.False);
		Assert.That (notifications, Is.GreaterThanOrEqualTo (3));
	}

	[Test]
	public void FillToolUsesAUnifiedOptionsObject ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		FillToolOptions options = canvas.GetFillToolOptions ();

		canvas.FillTolerance = 42;

		Assert.That (options.Tolerance, Is.EqualTo (42));
	}

	[Test]
	public void ActiveToolChangeRaisesToolOptionsChanged ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		int notifications = 0;
		canvas.ToolOptionsChanged += (_, _) => notifications++;

		canvas.ActiveTool = CanvasHost.CanvasTool.Text;

		Assert.That (notifications, Is.EqualTo (1));
	}

	[Test]
	public void TextToolTextChangeDoesNotRaiseToolOptionsChanged ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		int notifications = 0;
		canvas.ToolOptionsChanged += (_, _) => notifications++;

		canvas.TextToolText = "hello";

		Assert.That (notifications, Is.Zero);
		Assert.That (canvas.TextToolText, Is.EqualTo ("hello"));
	}

	[Test]
	public void GradientToolUsesAUnifiedOptionsObject ()
	{
		using var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2);
		var canvas = new CanvasHost (new Pinta.Core.Size (2, 2), surface);
		GradientToolOptions options = canvas.GetGradientToolOptions ();

		canvas.GradientReversed = true;

		Assert.That (options.Reversed, Is.True);
	}
}
