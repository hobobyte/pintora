using System;
using System.Collections.Generic;

namespace Pinta.Core;

public static class BrushAlgorithms
{
	public static IReadOnlyList<PointD> InterpolateLine (PointD from, PointD to, double density = 1)
	{
		double length = Math.Sqrt ((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
		int steps = Math.Max (1, (int) Math.Ceiling (Math.Max (length, 0) * density));
		var points = new List<PointD> (steps + 1);
		for (int i = 0; i <= steps; i++) {
			double progress = i / (double) steps;
			points.Add (new (
				from.X + (to.X - from.X) * progress,
				from.Y + (to.Y - from.Y) * progress));
		}
		return points;
	}

	/// <summary>Returns the normalized alpha coverage for a brush pixel.</summary>
	public static double HardnessCoverage (double edge, double hardness)
	{
		double softness = Math.Max (1 - hardness, 0.001);
		return Math.Clamp ((1 - edge) / softness, 0, 1);
	}
}
