namespace Pinta.Core;

/// <summary>Creates platform input method contexts for tools that edit text on the canvas.</summary>
public interface ITextInputContextFactory
{
	ITextInputContext CreateInputContext ();
}
