using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pintora.Avalonia.Document;

/// <summary>
/// Single place that applies the Save / Discard / Cancel policy when tabs are
/// closed, either individually, all at once, or as part of exiting the app.
/// </summary>
/// <remarks>
/// The coordinator never removes a tab whose save failed or whose prompt was
/// cancelled: a dirty document can only be dropped after an explicit
/// <see cref="CloseDecision.Discard"/> or a successful save. UI code supplies
/// the prompt and save delegates so this type stays headless and testable.
/// </remarks>
public sealed class DocumentCloseCoordinator
{
	private readonly DocumentTabManager tabs;
	private readonly Func<DocumentTab, Task<CloseDecision>> prompt;
	private readonly Func<DocumentTab, Task<bool>> save;

	/// <param name="tabs">The tab manager whose tabs are closed.</param>
	/// <param name="prompt">
	/// Asks the user how to handle a dirty tab. Must return
	/// <see cref="CloseDecision.Cancel"/> when the prompt is dismissed.
	/// </param>
	/// <param name="save">
	/// Persists a tab and returns <c>true</c> on success. Exceptions are
	/// treated as a failed save and keep the tab open.
	/// </param>
	public DocumentCloseCoordinator (
		DocumentTabManager tabs,
		Func<DocumentTab, Task<CloseDecision>> prompt,
		Func<DocumentTab, Task<bool>> save)
	{
		this.tabs = tabs ?? throw new ArgumentNullException (nameof (tabs));
		this.prompt = prompt ?? throw new ArgumentNullException (nameof (prompt));
		this.save = save ?? throw new ArgumentNullException (nameof (save));
	}

	/// <summary>
	/// Resolves the decision for a tab, performing the save when requested.
	/// Returns <c>false</c> when the tab must stay open (cancel or failed save).
	/// </summary>
	public async Task<bool> TryPrepareCloseAsync (DocumentTab tab)
	{
		if (tab is null)
			throw new ArgumentNullException (nameof (tab));
		if (!tab.IsDirty)
			return true;

		CloseDecision decision = await prompt (tab);
		switch (decision) {
			case CloseDecision.Cancel:
				return false;
			case CloseDecision.Discard:
				return true;
			case CloseDecision.Save:
				return await TrySaveAsync (tab);
			default:
				return false;
		}
	}

	/// <summary>
	/// Closes a single tab after applying the policy. Returns <c>true</c> when
	/// the tab was removed.
	/// </summary>
	public async Task<bool> CloseTabAsync (DocumentTab tab)
	{
		if (tab is null)
			throw new ArgumentNullException (nameof (tab));
		int index = tabs.Tabs.IndexOf (tab);
		if (index < 0)
			return false;
		if (!await TryPrepareCloseAsync (tab))
			return false;
		// The tab may have been closed by another path while the prompt was open.
		index = tabs.Tabs.IndexOf (tab);
		return index >= 0 && tabs.CloseTab (index, CloseDecision.Discard);
	}

	/// <summary>
	/// Saves every dirty tab. Returns <c>true</c> only when all saves succeed;
	/// tabs that fail to save remain dirty and open.
	/// </summary>
	public async Task<bool> SaveAllAsync ()
	{
		foreach (DocumentTab tab in tabs.Tabs.ToArray ()) {
			if (!tab.IsDirty)
				continue;
			if (!await TrySaveAsync (tab))
				return false;
		}
		return true;
	}

	/// <summary>
	/// Closes every tab, prompting for each dirty one. Stops at the first
	/// cancel or failed save so no dirty document is lost. Returns <c>true</c>
	/// when all tabs were closed.
	/// </summary>
	public async Task<bool> CloseAllAsync ()
	{
		foreach (DocumentTab tab in tabs.Tabs.ToArray ()) {
			if (!await CloseTabAsync (tab))
				return false;
		}
		return true;
	}

	/// <summary>
	/// Prepares the application to exit by closing every tab. Returns
	/// <c>true</c> when the window may close.
	/// </summary>
	public Task<bool> TryExitAsync ()
		=> CloseAllAsync ();

	private async Task<bool> TrySaveAsync (DocumentTab tab)
	{
		try {
			return await save (tab);
		} catch {
			// A throwing save must leave the tab open and dirty.
			return false;
		}
	}
}
