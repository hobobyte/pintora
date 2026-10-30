using System;

namespace Pinta.Core;

/// <summary>Platform-neutral gesture math used by transform tools.</summary>
public static class TransformAlgorithms
{
	public static Matrix3x2D TranslationMatrix (PointD offset)
		=> new (1, 0, 0, 1, offset.X, offset.Y);

	public static Matrix3x2D ScaleMatrix (PointD scale, PointD pivot)
	{
		return new (
			scale.X, 0,
			0, scale.Y,
			pivot.X * (1 - scale.X), pivot.Y * (1 - scale.Y));
	}

	public static Matrix3x2D RotationMatrix (RadiansAngle angle, PointD pivot)
		=> Matrix3x2D.CreateRotation (angle, pivot);

	public static PointD Translation (PointD original, PointD current)
		=> new (Math.Floor (current.X - original.X), Math.Floor (current.Y - original.Y));

	public static RadiansAngle Rotation (PointD original, PointD current, PointD center)
	{
		PointD first = original - center;
		PointD second = current - center;
		return new (Math.Atan2 (first.Y, first.X) - Math.Atan2 (second.Y, second.X));
	}

	public static PointD Scale (PointD original, PointD current, PointD center, bool constrain)
	{
		PointD delta = Translation (original, current);
		PointD first = original - center;
		double sx = (first.X + delta.X) / first.X;
		double sy = (first.Y + delta.Y) / first.Y;
		if (constrain) {
			double maximum = Math.Max (Math.Abs (sx), Math.Abs (sy));
			sx = maximum * Math.Sign (sx);
			sy = maximum * Math.Sign (sy);
		}
		return new (sx, sy);
	}
}
