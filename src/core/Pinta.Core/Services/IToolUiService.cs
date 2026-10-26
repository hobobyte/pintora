namespace Pinta.Core;

/// <summary>
/// Platform-neutral surface used by the tool manager to update the tool options UI.
/// Implementations own all platform widgets and layout details.
/// </summary>
public interface IToolUiService
{
	void ActivateTool (BaseTool tool);
	void DeactivateTool (BaseTool tool, BaseTool? nextTool);
}
