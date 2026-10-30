/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See third-party/paint.net/LICENSE.md for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

namespace Pinta.Core;

/// <summary>
/// How a new selection region combines with the current one
/// (GTK SelectionModeHandler: Replace/Union/Exclude/Xor/Intersect).
/// Lives in Core so both GTK and Avalonia adapters share the same semantics;
/// GUI layers only map input modifiers to these values.
/// </summary>
public enum SelectionCombineMode
{
	Replace,
	Add,
	Subtract,
	Intersect,
	Xor,
}
