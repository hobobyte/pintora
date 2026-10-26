using System;

namespace Pinta.Core;

internal static class ColorMath
{
	public static byte ClampToByte (double value)
		=> (byte) Math.Clamp (value, byte.MinValue, byte.MaxValue);

	public static byte LerpByte (byte from, byte to, byte fraction)
		=> (byte) (from + ((to - from) * fraction) / 255);

	public static T Lerp<T> (T from, T to, T fraction) where T : System.Numerics.IFloatingPoint<T>
		=> from + ((to - from) * fraction);
}
