namespace Pinta.Core;

/// <summary>
/// A renderer-neutral RGBA color with double-precision channels in the range [0, 1].
/// </summary>
public readonly record struct ColorD (
	double R,
	double G,
	double B,
	double A);
