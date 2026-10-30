//
// WindowActions.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

namespace Pinta.Core;

public sealed class WindowActions
{
	private IActionMenu doc_section = null!; // NRT - Set in RegisterActions
	private static readonly string doc_action_id = "active_document";
	private readonly IntStateCommand active_document;
	private IDisposable? deferred_update_timer;

	public Command SaveAll { get; }
	public Command CloseAll { get; }

	private readonly WorkspaceManager workspace;
	public WindowActions (WorkspaceManager workspace)
	{
		SaveAll = new Command (
			"SaveAll",
			Translations.GetString ("Save All"),
			null,
			Resources.StandardIcons.DocumentSave,
			shortcuts: ["<Ctrl><Alt>A"]);

		CloseAll = new Command (
			"CloseAll",
			Translations.GetString ("Close All"),
			null,
			Resources.StandardIcons.WindowClose,
			shortcuts: ["<Primary><Shift>W"]);

		active_document = new IntStateCommand (doc_action_id, -1);

		active_document.Activated += idx => workspace.SetActiveDocument (idx);

		workspace.DocumentActivated += (o, e) => {
			e.Document.Renamed += (_, _) => RebuildDocumentMenu ();
			e.Document.IsDirtyChanged += (_, _) => RebuildDocumentMenu ();
			AddDocumentMenuItem (workspace.OpenDocuments.IndexOf (e.Document));
		};

		workspace.ActiveDocumentChanged += OnActiveDocumentChanged;
		workspace.DocumentClosed += (_, _) => RebuildDocumentMenu ();

		this.workspace = workspace;
	}

	public void RegisterActions (
		IActionRegistrar registrar,
		IActionMenu menu)
	{
		menu.AppendItem (SaveAll);
		menu.AppendItem (CloseAll);

		doc_section = menu.CreateSubmenu ();
		menu.AppendSection (null, doc_section);

		registrar.AddCommands ([
			SaveAll,
			CloseAll]);

		registrar.AddIntStateCommand (active_document);

		// Assign accelerators up to Alt-9 for the active documents.
		for (int i = 0; i < 9; ++i)
			registrar.SetAccelerators (BuildActionId (i), [$"<Alt>{i + 1}"]);
	}

	private void AddDocumentMenuItem (int idx)
	{
		Document doc = workspace.OpenDocuments[idx];
		string actionId = BuildActionId (idx);
		string label = $"{doc.DisplayName}{(doc.IsDirty ? "*" : string.Empty)}";

		doc_section.AppendItem (new CommandMenuItem (label, actionId));
	}

	private static string BuildActionId (int idx) => $"app.{doc_action_id}({idx})";

	private void RebuildDocumentMenu ()
	{
		doc_section.RemoveAll ();

		for (int i = 0; i < workspace.OpenDocuments.Count; ++i)
			AddDocumentMenuItem (i);

		workspace.ResetTitle ();
	}

	private void OnActiveDocumentChanged (object? o, System.EventArgs eventArgs)
	{
		// Updating the action's state can be surprisingly expensive when e.g. opening
		// many documents (bug #1574), so an update is deferred until we return to the event loop.
		if (deferred_update_timer is not null)
			return;

		IUiScheduler uiScheduler = PintaCore.Services.GetService<IUiScheduler> ()
			?? throw new InvalidOperationException ("No IUiScheduler service is registered.");
		deferred_update_timer = uiScheduler.StartTimer (TimeSpan.FromMilliseconds (0), () => {
			active_document.Value = workspace.ActiveDocumentIndex;
			deferred_update_timer = null;
			return false;
		});
	}
}
