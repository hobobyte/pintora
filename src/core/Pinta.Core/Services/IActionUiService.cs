namespace Pinta.Core;

/// <summary>Platform-owned presentation of action toolbars and status information.</summary>
public interface IActionUiService
{
	void CreateToolBar (ActionManager actions, object toolbar);
	void CreateHeaderToolBar (ActionManager actions, object header);
	void CreateStatusBar (ActionManager actions, WorkspaceManager workspace, object statusbar);
}
