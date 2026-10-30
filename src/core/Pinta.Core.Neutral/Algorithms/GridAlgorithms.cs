using System;

namespace Pinta.Core;

public static class GridAlgorithms
{
	public static bool ShouldShow (bool enabled, int cellWidth, int cellHeight, double scale, int minimumViewDistance = 5)
	{
		if (!enabled || cellWidth <= 0 || cellHeight <= 0 || scale <= 0)
			return false;
		return (int) Math.Ceiling (Math.Min (cellWidth, cellHeight) * scale) >= minimumViewDistance;
	}

	public static int GetCellStep (int requested, int minimum, int maximum)
		=> Math.Clamp (requested, minimum, maximum);
}
