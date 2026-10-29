/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See third-party/paint.net/LICENSE.md for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Krzysztof Marecki <marecki.krzysztof@gmail.com>         //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;
using Cairo;
using Pinta.Core;

namespace Pinta.Effects;

public sealed class LevelsEffect : BaseEffect
{
	public sealed override bool IsTileable => true;

	public override string Icon => Pinta.Resources.Icons.AdjustmentsLevels;

	public override string Name => Translations.GetString ("Levels");

	public override bool IsConfigurable => true;

	public override string AdjustmentMenuKey => "L";

	public override string AdjustmentMenuKeyModifiers => "<Primary>";

	public LevelsData Data => (LevelsData) EffectData!;  // NRT - Set in constructor

	private readonly IEffectConfigurationService? configurationService;

	public LevelsEffect (IServiceProvider services)
	{
		configurationService = services.GetService<IEffectConfigurationService> ();

		EffectData = new LevelsData ();
	}

	public override Task<bool> LaunchConfiguration ()
		=> configurationService?.ConfigureAsync (this) ?? Task.FromResult (false);

	public override void Render (ImageSurface src, ImageSurface dest, ReadOnlySpan<RectangleI> rois)
		=> Data.Levels.Apply (dest, src, rois);
}

public sealed class LevelsData : EffectData
{
	public UnaryPixelOps.Level Levels { get; set; }

	public LevelsData ()
	{
		Levels = new UnaryPixelOps.Level ();
	}

	public override LevelsData Clone ()
		=> new () { Levels = (UnaryPixelOps.Level) Levels.Clone () };
}
