using System;

namespace Pinta.Core;

public static class SelectionAlgorithms
{
	public static PointD ClampPointToImage (PointD point, Size imageSize)
		=> new (
			Math.Round (Math.Clamp (point.X, 0, imageSize.Width)),
			Math.Round (Math.Clamp (point.Y, 0, imageSize.Height)));
}
