/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See third-party/paint.net/LICENSE.md for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Olivier Dufour <olivier.duff@gmail.com>                 //
//                     Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;
using Cairo;
using Pinta.Core;

namespace Pinta.Tools;

public sealed class TextTool : BaseTool
{
	// Variables for dragging
	private PointD start_mouse_xy;
	private PointI start_click_point;
	private bool tracking;

	private PointI click_point;
	private bool is_editing;
	private RectangleI old_cursor_bounds = RectangleI.Zero;

	//This is used to temporarily store the UserLayer's and TextLayer's previous ImageSurface states.
	private ImageSurface? text_undo_surface;
	private ImageSurface? user_undo_surface;
	private TextEngine? undo_engine;
	// The last pre-editing string, if pre-editing is active.
	private string? preedit_string;
	// The selection from when editing started. This ensures that text doesn't suddenly disappear/appear
	// if the selection changes before the text is finalized.
	private DocumentSelection? selection;

	private readonly ITextInputContext im_context;
	private readonly TextLayout layout;

	private RectangleI CurrentTextBounds {
		get => workspace.ActiveDocument.Layers.CurrentUserLayer.TextBounds;

		set {
			workspace.ActiveDocument.Layers.CurrentUserLayer.PreviousTextBounds = workspace.ActiveDocument.Layers.CurrentUserLayer.TextBounds;
			workspace.ActiveDocument.Layers.CurrentUserLayer.TextBounds = value;
		}
	}

	private TextEngine CurrentTextEngine {
		get {
			if (!workspace.HasOpenDocuments)
				throw new InvalidOperationException ("Attempting to get CurrentTextEngine when there are no open documents");

			return workspace.ActiveDocument.Layers.CurrentUserLayer.TextEngine;
		}
	}

	private TextLayout CurrentTextLayout {
		get {
			if (layout.Engine != CurrentTextEngine)
				layout.Engine = CurrentTextEngine;
			return layout;
		}
	}

	//While this is true, text will not be finalized upon Surface.Clone calls.
	private bool ignore_clone_finalizations = false;

	//Whether or not either (or both) of the Ctrl keys are pressed.
	private bool ctrl_key = false;

	//Store the most recent mouse position.
	private PointI last_mouse_position = new (0, 0);

	// Name of the cursor currently applied by UpdateMouseCursor (null = not yet applied).
	private string? current_cursor_name;

	public override string Name
		=> Translations.GetString ("Text");

	private static string FinalizeName
		=> Translations.GetString ("Text - Finalize");

	public override string Icon
		=> Pinta.Resources.Icons.ToolText;

	public override StandardKey? ShortcutKeyStandard
		=> StandardKey.T;

	public override int Priority
		=> 35;

	public override string StatusBarText
		=> Translations.GetString ("Left click to place cursor, then type desired text. Text color is primary color.");

	public override string DefaultCursorName
		=> Pinta.Resources.StandardCursors.Text;

	protected override bool ShowAntialiasingButton => true;

	private readonly IChromeService chrome;
	private readonly IPaletteService palette;
	private readonly IWorkspaceService workspace;
	public TextTool (IServiceProvider services) : base (services)
	{
		IChromeService chromeService = services.GetService<IChromeService> ();

		chrome = chromeService;
		palette = services.GetService<IPaletteService> ();
		workspace = services.GetService<IWorkspaceService> ();

		im_context = services.GetOptionalService<ITextInputContextFactory> ()?.CreateInputContext ()
			?? new NullTextInputContext ();

		im_context.Commit += OnIMCommit;
		im_context.PreeditStart += OnPreeditStart;
		im_context.PreeditChanged += OnPreeditChanged;
		im_context.PreeditEnd += OnPreeditEnd;

		layout = new TextLayout (chromeService);
	}

	/// <summary>
	/// Fallback input context used on platforms without an input method
	/// implementation: key events are not filtered, so no IME / pre-editing
	/// support is available (plain typing still works through the tool's
	/// regular key handling).
	/// </summary>
	#pragma warning disable CS0067 // Events are part of the contract but never raised by the null context.
	private sealed class NullTextInputContext : ITextInputContext
	{
		public void FocusIn () { }
		public void FocusOut () { }
		public void SetClientWidget (object? nativeWidget) { }
		public bool FilterKeyPress (object nativeKeyEvent) => false;
		public void Reset () { }
		public string GetPreeditString () => string.Empty;
		public event EventHandler<string>? Commit;
		public event EventHandler? PreeditStart;
		public event EventHandler? PreeditChanged;
		public event EventHandler? PreeditEnd;
	}
	#pragma warning restore CS0067

	#region ToolBar
	// NRT - Created by OnBuildToolBar
	private IToolBarLabel font_label = null!;
	private IToolBarFontButton font_button = null!;
	private IToolBarDropDownButton variant_btn = null!;
	private IToolBarSpinButton font_size = null!;
	private IToolBarDropDownButton weight_btn = null!;
	private IToolBarToggleButton italic_btn = null!;
	private IToolBarToggleButton underscore_btn = null!;
	private IToolBarToggleButton left_alignment_btn = null!;
	private IToolBarToggleButton center_alignment_btn = null!;
	private IToolBarToggleButton right_alignment_btn = null!;
	private IToolBarLabel fill_label = null!;
	private IToolBarDropDownButton fill_button = null!;
	private IToolBarSeparator fill_sep = null!;
	private IToolBarSeparator outline_sep = null!;
	private IToolBarSpinButton outline_width = null!;
	private IToolBarLabel outline_width_label = null!;
	private IToolBarSeparator join_sep = null!;
	private IToolBarDropDownButton join_btn = null!;

	protected override void OnBuildToolBar (IToolbarSurface tb)
	{
		if (font_label == null) {
			string fontText = Translations.GetString ("Font");
			font_label = WidgetFactory.CreateLabel ($" {fontText}: ");
		}

		tb.Append (font_label.NativeWidget);

		if (font_button == null) {
			font_button = WidgetFactory.CreateFontButton (Settings.GetSetting<string?> (SettingNames.TEXT_FONT, null));
			font_button.FontChanged += (o, e) => HandleFontChanged ();
		}

		tb.Append (font_button.NativeWidget);

		tb.Append (WidgetFactory.CreateSeparator ().NativeWidget);

		if (variant_btn == null) {
			variant_btn = CreateToolDropDownButton ();

			variant_btn.AddItem (
				// Translators: 'Normal' refers to the font-variant text property
				Translations.GetString ("Normal"),
				Pinta.Resources.Icons.TextVariantNormal,
				TextFontVariant.Normal
			);
			variant_btn.AddItem (
				// Translators: 'Small Caps' refers to the font-variant text property
				Translations.GetString ("Small Caps"),
				Pinta.Resources.Icons.TextVariantSmallCaps,
				TextFontVariant.SmallCaps
			);
			variant_btn.AddItem (
				// Translators: 'All Small Caps' refers to the font-variant text property
				Translations.GetString ("All Small Caps"),
				Pinta.Resources.Icons.TextVariantAllSmallCaps,
				TextFontVariant.AllSmallCaps
			);
			variant_btn.AddItem (
				// Translators: 'Petite Caps' refers to the font-variant text property
				Translations.GetString ("Petite Caps"),
				Pinta.Resources.Icons.TextVariantPetiteCaps,
				TextFontVariant.PetiteCaps
			);
			variant_btn.AddItem (
				// Translators: 'All Petite Caps' refers to the font-variant text property
				Translations.GetString ("All Petite Caps"),
				Pinta.Resources.Icons.TextVariantAllPetiteCaps,
				TextFontVariant.AllPetiteCaps
			);
			variant_btn.AddItem (
				// Translators: 'Unicase' refers to the font-variant text property
				Translations.GetString ("Unicase"),
				Pinta.Resources.Icons.TextVariantUnicase,
				TextFontVariant.Unicase
			);
			variant_btn.AddItem (
				// Translators: 'Title Caps' refers to the font-variant text property
				Translations.GetString ("Title Caps"),
				Pinta.Resources.Icons.TextVariantTitleCaps,
				TextFontVariant.Normal
			);

			variant_btn.SelectedIndex = Settings.GetSetting (SettingNames.TEXT_VARIANT, 0);
			variant_btn.SelectedItemChanged += HandleVariantButtonChanged;
		}

		tb.Append (variant_btn.NativeWidget);

		tb.Append (WidgetFactory.CreateSeparator ().NativeWidget);

		if (font_size == null) {
			font_size = WidgetFactory.CreateSpinButton (
				1,
				2000,
				1,
				PangoExtensions.UnitsToPixels (font_button.Font.SizePangoUnits));
			font_size.TooltipText = Translations.GetString ("Change font size. Shortcut keys: [ ]");
			font_size.TooltipText = Translations.GetString ("Change font size.") + "\n"
				   + "\n" + Translations.GetString ("Shortcut keys:")
				   + "\n" + Translations.GetString ("Press {0} to decrease font size", "\"[\"")
				   + "\n" + Translations.GetString ("Press {0} to increase font size", "\"]\"");
			font_size.ValueChanged += HandleFontSizeChanged;
		}

		tb.Append (font_size.NativeWidget);

		tb.Append (WidgetFactory.CreateSeparator ().NativeWidget);

		if (weight_btn == null) {
			weight_btn = CreateToolDropDownButton ();

			weight_btn.AddItem (
				// Translators: 'Thin' (100) refers to the font-weight text property
				Translations.GetString ("Thin") + " 100",
				Pinta.Resources.Icons.TextExtraLight,
				TextFontWeight.Thin
			);
			weight_btn.AddItem (
				// Translators: 'Ultralight' (200) refers to the font-weight text property
				Translations.GetString ("Ultralight") + " 200",
				Pinta.Resources.Icons.TextExtraLight,
				TextFontWeight.Ultralight
			);
			weight_btn.AddItem (
				// Translators: 'Light' (300) refers to the font-weight text property
				Translations.GetString ("Light") + " 300",
				Pinta.Resources.Icons.TextLight,
				TextFontWeight.Light
			);
			weight_btn.AddItem (
				// Translators: 'Semilight' (350) refers to the font-weight text property
				Translations.GetString ("Semilight") + " 350",
				Pinta.Resources.Icons.TextLight,
				TextFontWeight.Semilight
			);
			weight_btn.AddItem (
				// Translators: 'Book' (380) refers to the font-weight text property
				Translations.GetString ("Book") + " 380",
				Pinta.Resources.Icons.TextNormal,
				TextFontWeight.Book
			);
			weight_btn.AddItem (
				// Translators: 'Normal' (400) refers to the font-weight text property
				Translations.GetString ("Normal") + " 400",
				Pinta.Resources.Icons.TextNormal,
				TextFontWeight.Normal
			);
			weight_btn.AddItem (
				// Translators: 'Medium' (500) refers to the font-weight text property
				Translations.GetString ("Medium") + " 500",
				Pinta.Resources.Icons.TextNormal,
				TextFontWeight.Medium
			);
			weight_btn.AddItem (
				// Translators: 'Semibold' (600) refers to the font-weight text property
				Translations.GetString ("Semibold") + " 600",
				Pinta.Resources.Icons.TextBold,
				TextFontWeight.Semibold
			);
			weight_btn.AddItem (
				// Translators: 'Bold' (700) refers to the font-weight text property
				Translations.GetString ("Bold") + " 700",
				Pinta.Resources.Icons.TextBold,
				TextFontWeight.Bold
			);
			weight_btn.AddItem (
				// Translators: 'Ultrabold' (800) refers to the font-weight text property
				Translations.GetString ("Ultrabold") + " 800",
				Pinta.Resources.Icons.TextExtraBold,
				TextFontWeight.Ultrabold
			);
			weight_btn.AddItem (
				// Translators: 'Heavy' (900) refers to the font-weight text property
				Translations.GetString ("Heavy") + " 900",
				Pinta.Resources.Icons.TextExtraBold,
				TextFontWeight.Heavy
			);
			weight_btn.AddItem (
				// Translators: 'Ultraheavy' (1000) refers to the font-weight text property
				Translations.GetString ("Ultraheavy") + " 1000",
				Pinta.Resources.Icons.TextExtraBold,
				TextFontWeight.Ultraheavy
			);

			weight_btn.SelectedIndex = Settings.GetSetting (SettingNames.TEXT_WEIGHT, 5);
			weight_btn.SelectedItemChanged += HandleWeightButtonToggled;
		}

		tb.Append (weight_btn.NativeWidget);

		if (italic_btn == null) {
			italic_btn = WidgetFactory.CreateToggleButton (
				Pinta.Resources.StandardIcons.FormatTextItalic,
				Translations.GetString ("Italic"));
			italic_btn.Active = Settings.GetSetting (SettingNames.TEXT_ITALIC, false);
			italic_btn.Toggled += HandleItalicButtonToggled;
		}

		tb.Append (italic_btn.NativeWidget);

		if (underscore_btn == null) {
			underscore_btn = WidgetFactory.CreateToggleButton (
				Pinta.Resources.StandardIcons.FormatTextUnderline,
				Translations.GetString ("Underline"));
			underscore_btn.Active = Settings.GetSetting (SettingNames.TEXT_UNDERLINE, false);
			underscore_btn.Toggled += HandleUnderscoreButtonToggled;
		}

		tb.Append (underscore_btn.NativeWidget);

		tb.Append (WidgetFactory.CreateSeparator ().NativeWidget);

		TextAlignment alignment = (TextAlignment) Settings.GetSetting (SettingNames.TEXT_ALIGNMENT, (int) TextAlignment.Left);

		if (left_alignment_btn == null) {
			left_alignment_btn = WidgetFactory.CreateToggleButton (
				Pinta.Resources.StandardIcons.FormatJustifyLeft,
				Translations.GetString ("Left Align"));
			left_alignment_btn.Active = alignment == TextAlignment.Left;
			left_alignment_btn.Toggled += HandleLeftAlignmentButtonToggled;
		}

		tb.Append (left_alignment_btn.NativeWidget);

		if (center_alignment_btn == null) {
			center_alignment_btn = WidgetFactory.CreateToggleButton (
				Pinta.Resources.StandardIcons.FormatJustifyCenter,
				Translations.GetString ("Center Align"));
			center_alignment_btn.Active = alignment == TextAlignment.Center;
			center_alignment_btn.Toggled += HandleCenterAlignmentButtonToggled;
		}

		tb.Append (center_alignment_btn.NativeWidget);

		if (right_alignment_btn == null) {
			right_alignment_btn = WidgetFactory.CreateToggleButton (
				Pinta.Resources.StandardIcons.FormatJustifyRight,
				Translations.GetString ("Right Align"));
			right_alignment_btn.Active = alignment == TextAlignment.Right;
			right_alignment_btn.Toggled += HandleRightAlignmentButtonToggled;
		}

		tb.Append (right_alignment_btn.NativeWidget);

		fill_sep ??= WidgetFactory.CreateSeparator ();

		tb.Append (fill_sep.NativeWidget);

		if (fill_label == null) {
			string textStyleText = Translations.GetString ("Text Style");
			fill_label = WidgetFactory.CreateLabel ($" {textStyleText}: ");
		}

		tb.Append (fill_label.NativeWidget);

		if (fill_button == null) {
			fill_button = CreateToolDropDownButton ();

			fill_button.AddItem (Translations.GetString ("Normal"), Pinta.Resources.Icons.FillStyleFill, 0);
			fill_button.AddItem (Translations.GetString ("Normal and Outline"), Pinta.Resources.Icons.FillStyleOutlineFill, 1);
			fill_button.AddItem (Translations.GetString ("Outline"), Pinta.Resources.Icons.FillStyleOutline, 2);
			fill_button.AddItem (Translations.GetString ("Fill Background"), Pinta.Resources.Icons.FillStyleBackground, 3);

			fill_button.SelectedIndex = Settings.GetSetting (SettingNames.TEXT_STYLE, 0);
			fill_button.SelectedItemChanged += HandleFillButtonToggled;
		}

		tb.Append (fill_button.NativeWidget);

		outline_sep ??= WidgetFactory.CreateSeparator ();

		tb.Append (outline_sep.NativeWidget);

		if (outline_width_label == null) {
			string outlineWidthText = Translations.GetString ("Outline width");
			outline_width_label = WidgetFactory.CreateLabel ($" {outlineWidthText}: ");
		}

		tb.Append (outline_width_label.NativeWidget);

		if (outline_width == null) {
			outline_width = WidgetFactory.CreateSpinButton (
				1,
				1e5,
				1,
				Settings.GetSetting (SettingNames.TEXT_OUTLINE_WIDTH, 2));
			outline_width.ValueChanged += (_, __) => HandleFontChanged ();
		}

		tb.Append (outline_width.NativeWidget);

		join_sep ??= WidgetFactory.CreateSeparator ();

		tb.Append (join_sep.NativeWidget);

		if (join_btn == null) {
			join_btn = CreateToolDropDownButton ();

			join_btn.AddItem (
				// Translators: 'Miter Join' refers to the Cairo.LineJoin property
				Translations.GetString ("Miter Join"),
				Pinta.Resources.Icons.JoinMiter,
				Cairo.LineJoin.Miter
			);
			join_btn.AddItem (
				// Translators: 'Round Join' refers to the Cairo.LineJoin property
				Translations.GetString ("Round Join"),
				Pinta.Resources.Icons.JoinRound,
				Cairo.LineJoin.Round
			);
			join_btn.AddItem (
				// Translators: 'Bevel Join' refers to the Cairo.LineJoin property
				Translations.GetString ("Bevel Join"),
				Pinta.Resources.Icons.JoinBevel,
				Cairo.LineJoin.Bevel
			);

			join_btn.SelectedIndex = Settings.GetSetting (SettingNames.TEXT_JOIN, 0);
			join_btn.SelectedItemChanged += HandleJoinButtonToggled;
		}

		tb.Append (join_btn.NativeWidget);

		outline_width.Visible = outline_width_label.Visible = outline_sep.Visible = join_btn.Visible = join_sep.Visible = StrokeText;

		UpdateFont ();
	}

	private void HandleFontSizeChanged (object? sender, EventArgs e)
	{
		font_button.Font = font_button.Font with {
			SizePangoUnits = PangoExtensions.UnitsFromPixels ((int) font_size.Value)
		};

		UpdateFont ();
	}

	protected override void OnSaveSettings (ISettingsService settings)
	{
		base.OnSaveSettings (settings);

		if (font_button is not null)
			settings.PutSetting (SettingNames.TEXT_FONT, font_button.Font.ToString ()!);

		if (variant_btn is not null)
			settings.PutSetting (SettingNames.TEXT_VARIANT, variant_btn.SelectedIndex);

		if (weight_btn is not null)
			settings.PutSetting (SettingNames.TEXT_WEIGHT, weight_btn.SelectedIndex);

		if (italic_btn is not null)
			settings.PutSetting (SettingNames.TEXT_ITALIC, italic_btn.Active);

		if (underscore_btn is not null)
			settings.PutSetting (SettingNames.TEXT_UNDERLINE, underscore_btn.Active);

		if (left_alignment_btn is not null)
			settings.PutSetting (SettingNames.TEXT_ALIGNMENT, (int) Alignment);

		if (fill_button is not null)
			settings.PutSetting (SettingNames.TEXT_STYLE, fill_button.SelectedIndex);

		if (outline_width is not null)
			settings.PutSetting (SettingNames.TEXT_OUTLINE_WIDTH, (int) outline_width.Value);

		if (join_btn is not null)
			settings.PutSetting (SettingNames.TEXT_JOIN, join_btn.SelectedIndex);
	}

	private void HandleFontChanged ()
	{
		font_button.Font = font_button.Font with {
			SizePangoUnits = PangoExtensions.UnitsFromPixels ((int) font_size.Value)
		};

		if (workspace.HasOpenDocuments)
			workspace.ActiveDocument.Workspace.GrabFocusToCanvas ();

		UpdateFont ();
	}

	private void HandleVariantButtonChanged (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	private TextAlignment Alignment {
		get {
			if (right_alignment_btn.Active)
				return TextAlignment.Right;
			else if (center_alignment_btn.Active)
				return TextAlignment.Center;
			else
				return TextAlignment.Left;
		}
	}

	private void HandlePintaCorePalettePrimaryColorChanged (object? sender, EventArgs e)
	{
		UpdateTextEngineColor ();
		if (is_editing || (workspace.HasOpenDocuments && CurrentTextEngine.State == TextMode.NotFinalized))
			RedrawText (is_editing, true);
	}

	private void HandleLeftAlignmentButtonToggled (object? sender, EventArgs e)
	{
		if (left_alignment_btn.Active) {
			right_alignment_btn.Active = false;
			center_alignment_btn.Active = false;
		} else if (!right_alignment_btn.Active && !center_alignment_btn.Active) {
			left_alignment_btn.Active = true;
		}

		UpdateFont ();
	}

	private void HandleCenterAlignmentButtonToggled (object? sender, EventArgs e)
	{
		if (center_alignment_btn.Active) {
			right_alignment_btn.Active = false;
			left_alignment_btn.Active = false;
		} else if (!right_alignment_btn.Active && !left_alignment_btn.Active) {
			center_alignment_btn.Active = true;
		}

		UpdateFont ();
	}

	private void HandleRightAlignmentButtonToggled (object? sender, EventArgs e)
	{
		if (right_alignment_btn.Active) {
			center_alignment_btn.Active = false;
			left_alignment_btn.Active = false;
		} else if (!center_alignment_btn.Active && !left_alignment_btn.Active) {
			right_alignment_btn.Active = true;
		}

		UpdateFont ();
	}

	private void HandleUnderscoreButtonToggled (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	private void HandleItalicButtonToggled (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	private void HandleWeightButtonToggled (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	private void HandleFillButtonToggled (object? sender, EventArgs e)
	{
		outline_width.Visible = outline_width_label.Visible = outline_sep.Visible = join_btn.Visible = join_sep.Visible = StrokeText;

		UpdateFont ();
	}

	private void HandleJoinButtonToggled (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	private void HandleSelectedLayerChanged (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	protected override void OnAntialiasingChanged ()
	{
		UpdateFont ();
	}

	private void UpdateFont ()
	{
		if (workspace.HasOpenDocuments) {

			var font = font_button.Font with {
				Variant = variant_btn.SelectedItem.GetTagOrDefault (TextFontVariant.Normal),
				Weight = weight_btn.SelectedItem.GetTagOrDefault (TextFontWeight.Normal),
				Style = italic_btn.Active ? TextFontStyle.Italic : TextFontStyle.Normal,
			};

			CurrentTextEngine.SetFont (font, Alignment, underscore_btn.Active);
		}

		if (is_editing || (workspace.HasOpenDocuments && CurrentTextEngine.State == TextMode.NotFinalized))
			RedrawText (is_editing, true);
	}

	private void UpdateTextEngineColor ()
	{
		if (!workspace.HasOpenDocuments) return;
		CurrentTextEngine.PrimaryColor = palette.PrimaryColor;
		CurrentTextEngine.SecondaryColor = palette.SecondaryColor;
	}

	private int OutlineWidth
		=> (int) outline_width.Value;

	private bool StrokeText
		=> fill_button.SelectedItem.GetTagOrDefault (0) >= 1 && fill_button.SelectedItem.GetTagOrDefault (0) != 3;

	private bool FillText
		=> fill_button.SelectedItem.GetTagOrDefault (0) <= 1 || fill_button.SelectedItem.GetTagOrDefault (0) == 3;

	private bool BackgroundFill
		=> fill_button.SelectedItem.GetTagOrDefault (0) == 3;

	#endregion

	#region Activation/Deactivation
	protected override void OnActivated (Document? document)
	{
		base.OnActivated (document);

		// We may need to redraw our text when the color changes
		palette.PrimaryColorChanged += HandlePintaCorePalettePrimaryColorChanged;
		palette.SecondaryColorChanged += HandlePintaCorePalettePrimaryColorChanged;

		workspace.LayerAdded += HandleSelectedLayerChanged;
		workspace.LayerRemoved += HandleSelectedLayerChanged;
		workspace.SelectedLayerChanged += HandleSelectedLayerChanged;

		// We always start off not in edit mode
		is_editing = false;
	}

	protected override void OnCommit (Document? document)
	{
		im_context.FocusOut ();
		StopEditing (false);
	}

	protected override void OnDeactivated (Document? document, BaseTool? newTool)
	{
		base.OnDeactivated (document, newTool);

		// Stop listening for color change events
		palette.PrimaryColorChanged -= HandlePintaCorePalettePrimaryColorChanged;
		palette.SecondaryColorChanged -= HandlePintaCorePalettePrimaryColorChanged;

		workspace.LayerAdded -= HandleSelectedLayerChanged;
		workspace.LayerRemoved -= HandleSelectedLayerChanged;
		workspace.SelectedLayerChanged -= HandleSelectedLayerChanged;

		StopEditing (false);
	}
	#endregion

	#region Mouse Handlers
	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		ctrl_key = e.IsControlPressed;
		im_context.FocusIn (); // Grab focus so we can get keystrokes
		selection = document.Selection.Clone ();

		switch (e.MouseButton) {
			case MouseButton.Right:
				HandleRightClick (document, e);
				break;
			case MouseButton.Left:
				HandleLeftClick (document, e);
				break;
		}
	}

	private void HandleLeftClick (Document document, ToolMouseEventArgs e)
	{
		//Store the mouse position.
		PointI pt = e.Point;

		// If the user is [editing or holding down Ctrl] and clicked
		//within the text, move the cursor to the click location
		if ((is_editing || ctrl_key) && CurrentTextBounds.Contains (pt)) {
			StartEditing ();

			//Change the position of the cursor to where the mouse clicked.
			TextPosition p = CurrentTextLayout.PointToTextPosition (pt);
			CurrentTextEngine.SetCursorPosition (p, true);

			//Redraw the text with the new cursor position.
			RedrawText (true, true);

			return;
		}

		// We're already editing and the user clicked outside the text,
		// commit the user's work, and start a new edit
		switch (CurrentTextEngine.State) {
			// We were editing, save and stop
			case TextMode.Uncommitted:
				StopEditing (true);
				break;

			// We were editing, but nothing had been
			// keyed. Stop editing.
			case TextMode.Unchanged:
				StopEditing (false);
				break;
		}

		if (ctrl_key) {
			//Go through every UserLayer.
			foreach (UserLayer ul in document.Layers.UserLayers) {
				//Check each UserLayer's editable text boundaries to see if they contain the mouse position.
				if (!ul.TextBounds.Contains (pt))
					continue;

				//The mouse clicked on editable text.

				//Change the current UserLayer to the Layer that contains the text that was clicked on.
				document.Layers.SetCurrentUserLayer (ul);

				//The user is editing text now.
				is_editing = true;

				//Set the cursor in the editable text where the mouse was clicked.
				TextPosition p = CurrentTextLayout.PointToTextPosition (pt);
				CurrentTextEngine.SetCursorPosition (p, true);

				//Redraw the editable text with the cursor.
				RedrawText (true, true);

				//Don't check any more UserLayers - stop at the first UserLayer that has editable text containing the mouse position.
				return;
			}
		} else {
			if (CurrentTextEngine.State == TextMode.NotFinalized) {
				//The user is making a new text and the old text hasn't been finalized yet.
				FinalizeText ();
			}

			if (is_editing)
				return;

			// Start editing at the cursor location
			click_point = pt;
			CurrentTextEngine.Clear ();
			UpdateFont ();
			click_point = click_point with { Y = click_point.Y - (CurrentTextLayout.FontHeight / 2) };
			CurrentTextEngine.Origin = click_point;
			StartEditing ();
			RedrawText (true, true);
		}
	}

	private void HandleRightClick (Document document, ToolMouseEventArgs e)
	{
		// A right click allows you to move the text around

		//The user is dragging text with the right mouse button held down, so track the mouse as it moves.
		tracking = true;

		//Remember the position of the mouse before the text is dragged.
		start_mouse_xy = e.PointDouble;
		start_click_point = click_point;

		//Change the cursor to indicate that the text is being dragged.
		UpdateMouseCursor (document);
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		ctrl_key = e.IsControlPressed;

		last_mouse_position = e.Point;

		// If we're dragging the text around, do that
		if (tracking) {
			PointD delta = new (
				e.PointDouble.X - start_mouse_xy.X,
				e.PointDouble.Y - start_mouse_xy.Y);

			click_point = new PointI ((int) (start_click_point.X + delta.X), (int) (start_click_point.Y + delta.Y));
			CurrentTextEngine.Origin = click_point;

			RedrawText (true, true);
		} else {
			UpdateMouseCursor (document);
		}
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		// If we were dragging the text around, finish that up
		if (!tracking)
			return;

		PointD delta = new (e.PointDouble.X - start_mouse_xy.X, e.PointDouble.Y - start_mouse_xy.Y);

		click_point = new PointI ((int) (start_click_point.X + delta.X), (int) (start_click_point.Y + delta.Y));
		CurrentTextEngine.Origin = click_point;

		RedrawText (false, true);
		tracking = false;
		UpdateMouseCursor (document);
	}

	private void UpdateMouseCursor (Document document)
	{
		if (tracking) {
			ApplyCursor (Pinta.Resources.StandardCursors.Move);
			return;
		}

		//Whether or not to show the normal text cursor.
		string newCursorName;

		if (ctrl_key && workspace.HasOpenDocuments) {
			//Go through every UserLayer.
			newCursorName = Pinta.Resources.StandardCursors.NotAllowed;
			foreach (UserLayer ul in document.Layers.UserLayers) {
				if (!ul.TextBounds.Contains (last_mouse_position)) continue; //Check each UserLayer's editable text boundaries to see if they contain the mouse position.
				newCursorName = DefaultCursorName; //The mouse is over editable text.
			}
		} else {
			newCursorName = DefaultCursorName;
		}

		if (newCursorName != current_cursor_name) {
			ApplyCursor (newCursorName);
			RedrawText (is_editing, true);
		}
	}

	private void ApplyCursor (string cursorName)
	{
		SetCursor (cursorName);
		current_cursor_name = cursorName;
	}
	#endregion

	#region Keyboard Handlers

	protected override bool OnKeyDown (Document document, ToolKeyEventArgs e)
	{
		if (!workspace.HasOpenDocuments)
			return false;

		// If we are dragging the text, we
		// aren't going to handle key presses
		if (tracking)
			return false;

		// Ignore anything with Alt pressed
		if (e.IsAltPressed)
			return false;

		ctrl_key = e.Key.IsControlKey ();
		UpdateMouseCursor (document);

		bool keyHandled = false;
		if (is_editing) {
			if (preedit_string is not null && e.Event is not null) {
				// When pre-editing is active, the input method should consume all keystrokes first.
				// (e.g. Enter might be used to finish pre-editing)
				keyHandled = TryHandleChar (e.Event);
			}

			if (!keyHandled) {
				// Assume that we are going to handle the key
				keyHandled = true;

				if (e.Key.Is (StandardKey.BackSpace)) {
					CurrentTextEngine.PerformBackspace (e.IsControlPressed);
				} else if (e.Key.Is (StandardKey.Delete)) {
					CurrentTextEngine.PerformDelete ();
				} else if (e.Key.Is (StandardKey.KeypadEnter) || e.Key.Is (StandardKey.Return)) {
					CurrentTextEngine.PerformEnter ();
				} else if (e.Key.Is (StandardKey.Left)) {
					CurrentTextEngine.PerformLeft (e.IsControlPressed, e.IsShiftPressed);
				} else if (e.Key.Is (StandardKey.Right)) {
					CurrentTextEngine.PerformRight (e.IsControlPressed, e.IsShiftPressed);
				} else if (e.Key.Is (StandardKey.Up)) {
					CurrentTextEngine.PerformUp (e.IsShiftPressed);
				} else if (e.Key.Is (StandardKey.Down)) {
					CurrentTextEngine.PerformDown (e.IsShiftPressed);
				} else if (e.Key.Is (StandardKey.Home)) {
					CurrentTextEngine.PerformHome (e.IsControlPressed, e.IsShiftPressed);
				} else if (e.Key.Is (StandardKey.End)) {
					CurrentTextEngine.PerformEnd (e.IsControlPressed, e.IsShiftPressed);
				} else if (e.Key.Is (StandardKey.PageUp) || e.Key.Is (StandardKey.PageDown)) {
					// Not handled.
				} else if (e.Key.Is (StandardKey.Escape)) {
					StopEditing (false);
					return true;
				} else if (e.Key.Is (StandardKey.Insert)) {
					if (e.IsShiftPressed) {
						CurrentTextEngine.PerformPaste (ClipboardService).Wait ();
					} else if (e.IsControlPressed) {
						CurrentTextEngine.PerformCopy (ClipboardService);
					}
				} else if (e.IsControlPressed) {
					if (e.Key.Is (StandardKey.Z)) {
						//Ctrl + Z for undo while editing.
						OnHandleUndo (document);

						if (workspace.ActiveDocument.History.CanUndo)
							workspace.ActiveDocument.History.Undo ();

						return true;
					} else if (e.Key.Is (StandardKey.I)) {
						italic_btn.Active = !italic_btn.Active;
						UpdateFont ();
					} else if (e.Key.Is (StandardKey.B)) {
						// If current font-weight is Bold (8) or bolder, set to Normal (5). Otherwise, set to Bold (8).
						weight_btn.SelectedIndex = weight_btn.SelectedIndex > 7 ? 5 : 8;
						UpdateFont ();
					} else if (e.Key.Is (StandardKey.U)) {
						underscore_btn.Active = !underscore_btn.Active;
						UpdateFont ();
					} else if (e.Key.Is (StandardKey.A)) {
						// Select all of the text.
						CurrentTextEngine.PerformHome (true, false);
						CurrentTextEngine.PerformEnd (true, true);
					} else {
						//Ignore command shortcut.
						return false;
					}
				} else {
					keyHandled = e.Event is not null && TryHandleChar (e.Event);
					if (!keyHandled)
						keyHandled = InsertFallbackText (e.Text);
				}
			}

			if (keyHandled)
				RedrawText (true, true);
		} else {
			if (e.Key.Is (StandardKey.BracketLeft)) {
				font_size.Value--;
				return true;
			}
			if (e.Key.Is (StandardKey.BracketRight)) {
				font_size.Value++;
				return true;
			}
		}

		return keyHandled;
	}

	protected override bool OnKeyUp (Document document, ToolKeyEventArgs e)
	{
		if (!e.Key.IsControlKey () && !e.IsControlPressed)
			return false;

		ctrl_key = false;

		UpdateMouseCursor (document);
		return false;
	}

	private bool TryHandleChar (object eventKey)
	{
		// Try to handle it as a character
		if (im_context.FilterKeyPress (eventKey))
			return true;

		// We didn't handle the key
		return false;
	}

	private bool InsertFallbackText (string? text)
	{
		if (string.IsNullOrEmpty (text))
			return false;

		CurrentTextEngine.InsertText (text);
		return true;
	}

	private void OnIMCommit (object? o, string str)
	{
		try {
			// Reset the pre-edit string. Depending on the platform there might still be
			// a preedit-changed signal (setting it to the empty string) after the commit, rather than before.
			UpdatePreeditString (string.Empty, redraw: false);

			CurrentTextEngine.InsertText (str);
			RedrawText (true, true);
		} finally {
			im_context.Reset ();
		}
	}

	private void OnPreeditStart (object? o, EventArgs args)
	{
		// Initialize to empty string (null means pre-editing is inactive).
		preedit_string = string.Empty;
	}

	private void OnPreeditEnd (object? o, EventArgs args)
	{
		// Reset to indicate that pre-editing is done. There should have previously been
		// a preedit-changed signal to erase the last preedited string.
		preedit_string = null;
	}

	private void OnPreeditChanged (object? o, EventArgs args)
	{
		// TODO - use the Pango.AttrList argument to better visualize the pre-edited text vs the regular text.
		UpdatePreeditString (im_context.GetPreeditString (), redraw: true);
	}

	private void UpdatePreeditString (string updated, bool redraw)
	{
		// Remove the previous preedit string.
		for (int i = 0; i < preedit_string?.Length; ++i)
			CurrentTextEngine.PerformBackspace (false);

		// Insert the new string.
		preedit_string = updated;
		CurrentTextEngine.InsertText (preedit_string);

		RedrawText (true, true);
	}

	#endregion

	#region Start/Stop Editing

	private void StartEditing ()
	{
		// Ensure we have an event handler added to finalize re-editable text for the document if the layer is cloned.
		workspace.ActiveDocument.LayerCloned -= FinalizeText;
		workspace.ActiveDocument.LayerCloned += FinalizeText;

		is_editing = true;

		im_context.SetClientWidget (workspace.ActiveWorkspace.Canvas);

		selection ??= workspace.ActiveDocument.Selection.Clone ();

		//Start ignoring any Surface.Clone calls from this point on (so that it doesn't start to loop).
		ignore_clone_finalizations = true;

		//Store the previous state of the current UserLayer's and TextLayer's ImageSurfaces.
		user_undo_surface = workspace.ActiveDocument.Layers.CurrentUserLayer.CairoSurface.Clone ();
		text_undo_surface = workspace.ActiveDocument.Layers.CurrentUserLayer.TextLayer.Layer.CairoSurface.Clone ();

		//Store the previous state of the Text Engine.
		undo_engine = CurrentTextEngine.Clone ();

		//Update Text Engine to use current colors of color palette
		UpdateTextEngineColor ();

		//Stop ignoring any Surface.Clone calls from this point on.
		ignore_clone_finalizations = false;
	}

	private void StopEditing (bool finalize)
	{
		im_context.SetClientWidget (null);

		if (!workspace.HasOpenDocuments)
			return;

		if (!is_editing)
			return;

		is_editing = false;

		//Make sure that neither undo surface is null, the user is editing, and there are uncommitted changes.
		if (text_undo_surface != null && user_undo_surface != null && CurrentTextEngine.State == TextMode.Uncommitted) {
			Document doc = workspace.ActiveDocument;

			RedrawText (false, true);

			//Start ignoring any Surface.Clone calls from this point on (so that it doesn't start to loop).
			ignore_clone_finalizations = true;

			//Create a new TextHistoryItem so that the committing of text can be undone.
			doc.History.PushNewItem (
				new TextHistoryItem (
					workspace,
					Icon,
					Name,
					text_undo_surface.Clone (),
					user_undo_surface.Clone (),
					undo_engine!.Clone (), // NRT - Set in StartEditing
					doc.Layers.CurrentUserLayer
				)
			);

			//Stop ignoring any Surface.Clone calls from this point on.
			ignore_clone_finalizations = false;

			//Now that the text has been committed, change its state.
			CurrentTextEngine.State = TextMode.NotFinalized;
		}

		RedrawText (false, true);

		if (finalize) {
			FinalizeText ();
		}
	}
	#endregion

	#region Text Drawing Methods
	/// <summary>
	/// Clears the entire TextLayer and redraw the previous text boundary.
	/// </summary>
	private void ClearTextLayer ()
	{
		//Clear the TextLayer.
		workspace.ActiveDocument.Layers.CurrentUserLayer.TextLayer.Layer.CairoSurface.Clear ();

		//Redraw the previous text boundary.
		InflateAndInvalidate (workspace.ActiveDocument.Layers.CurrentUserLayer.PreviousTextBounds);
	}

	/// <summary>
	/// Draws the text.
	/// </summary>
	/// <param name="showCursor">Whether or not to show the mouse cursor in the drawing.</param>
	/// <param name="useTextLayer">Whether or not to use the TextLayer (as opposed to the Userlayer).</param>
	private void RedrawText (bool showCursor, bool useTextLayer)
	{
		RectangleI r =
			CurrentTextLayout
			.GetLayoutBounds ()
			.Inflated (10 + OutlineWidth, 10 + OutlineWidth);

		InflateAndInvalidate (r);
		CurrentTextBounds = r;

		RectangleI cursorBounds = RectangleI.Zero;

		ImageSurface surf;

		if (!useTextLayer) {
			//Draw text on the current UserLayer's surface as finalized text.
			surf = workspace.ActiveDocument.Layers.CurrentUserLayer.CairoSurface;
		} else {
			//Draw text on the current UserLayer's TextLayer's surface as re-editable text.
			surf = workspace.ActiveDocument.Layers.CurrentUserLayer.TextLayer.Layer.CairoSurface;

			ClearTextLayer ();
		}

		using Context g = new (surf);

		FontOptions options = new ();

		if (UseAntialiasing) {
			g.Antialias = Antialias.Gray; // Adjusts antialiasing JUST for the outline brush
			options.Antialias = Antialias.Gray; // Adjusts antialiasing for PangoCairo's text draw function
		} else {
			g.Antialias = Antialias.None;
			options.Antialias = Antialias.None;
		}

		g.Save ();
		PangoCairo.Functions.ContextSetFontOptions ((Pango.Context) chrome.TextContext, options);


		// Show selection if on text layer

		if (useTextLayer) {

			// Selected Text

			Color c = new (
				R: 0.7,
				G: 0.8,
				B: 0.9,
				A: 0.5);

			foreach (RectangleI rect in CurrentTextLayout.GetSelectionRectangles ())
				g.FillRectangle (rect.ToDouble (), c);
		}

		selection?.Clip (g);

		g.MoveTo (CurrentTextEngine.Origin.X, CurrentTextEngine.Origin.Y);

		g.SetSourceColor (CurrentTextEngine.PrimaryColor);

		//Fill in background
		if (BackgroundFill) {
			using Context g2 = new (surf);
			selection?.Clip (g2);
			g2.FillRectangle (CurrentTextLayout.GetLayoutBounds ().ToDouble (), CurrentTextEngine.SecondaryColor);
		}

		// Draws the text stroke
		if (StrokeText) {
			g.SetSourceColor (FillText ? CurrentTextEngine.SecondaryColor : CurrentTextEngine.PrimaryColor);
			g.LineWidth = OutlineWidth;
			g.LineJoin = (Cairo.LineJoin) join_btn.SelectedIndex;

			PangoCairo.Functions.LayoutPath (g, CurrentTextLayout.Layout);
			g.Stroke ();

			// Position resets after g.Stroke ();
			if (FillText) {
				g.MoveTo (CurrentTextEngine.Origin.X, CurrentTextEngine.Origin.Y);
				g.SetSourceColor (CurrentTextEngine.PrimaryColor);
			}
		}

		// Draws the text fill
		if (FillText) {
			PangoCairo.Functions.ShowLayout (g, CurrentTextLayout.Layout);
		}

		if (showCursor) {

			RectangleI loc = CurrentTextLayout.GetCursorLocation ();
			Color color = CurrentTextEngine.PrimaryColor;

			g.DrawLine (
				new PointD (loc.X, loc.Y),
				new PointD (loc.X, loc.Y + loc.Height),
				color, 1);

			cursorBounds = loc;
			cursorBounds = cursorBounds.Inflated (2, 10);
		}

		g.Restore ();


		if (useTextLayer && (is_editing || ctrl_key) && !CurrentTextEngine.IsEmpty ()) {

			//Draw the text edit rectangle.

			g.Save ();

			g.Translate (.5, .5);

			g.AppendPath (g.CreateRectanglePath (CurrentTextBounds.ToDouble ()));

			g.LineWidth = 1;

			g.SetSourceColor (new Color (1, 1, 1));
			g.StrokePreserve ();

			g.SetDash ([2, 4], 0);
			g.SetSourceColor (new Color (1, .1, .2));

			g.Stroke ();

			g.Restore ();
		}

		InflateAndInvalidate (workspace.ActiveDocument.Layers.CurrentUserLayer.PreviousTextBounds);
		workspace.Invalidate (old_cursor_bounds);
		InflateAndInvalidate (r);
		workspace.Invalidate (cursorBounds);

		old_cursor_bounds = cursorBounds;
	}

	/// <summary>
	/// Finalize re-editable text (if applicable).
	/// </summary>
	public void FinalizeText ()
	{
		//If this is true, don't finalize any text - this is used to prevent the code from looping recursively.
		if (ignore_clone_finalizations)
			return;

		//Only bother finalizing text if editing.
		if (CurrentTextEngine.State == TextMode.Unchanged)
			return;

		//Start ignoring any Surface.Clone calls from this point on (so that it doesn't start to loop).
		ignore_clone_finalizations = true;
		Document doc = workspace.ActiveDocument;

		//Create a backup of everything before redrawing the text and etc.
		ImageSurface oldTextSurface = doc.Layers.CurrentUserLayer.TextLayer.Layer.CairoSurface.Clone ();
		ImageSurface oldUserSurface = doc.Layers.CurrentUserLayer.CairoSurface.Clone ();
		TextEngine oldTextEngine = CurrentTextEngine.Clone ();

		//Draw the text onto the UserLayer (without the cursor) rather than the TextLayer.
		RedrawText (false, false);

		//Clear the TextLayer.
		doc.Layers.CurrentUserLayer.TextLayer.Layer.Clear ();

		//Clear the text and its boundaries.
		CurrentTextEngine.Clear ();
		CurrentTextBounds = RectangleI.Zero;

		//Create a new TextHistoryItem so that the finalization of the text can be undone. Construct
		//it on the spot so that it is more memory efficient if the changes are small.
		TextHistoryItem hist = new (
			workspace,
			Icon,
			FinalizeName,
			oldTextSurface,
			oldUserSurface,
			oldTextEngine,
			doc.Layers.CurrentUserLayer);

		//Add the new TextHistoryItem.
		doc.History.PushNewItem (hist);

		//Stop ignoring any Surface.Clone calls from this point on.
		ignore_clone_finalizations = false;

		//Now that the text has been finalized, change its state.
		CurrentTextEngine.State = TextMode.Unchanged;

		selection = null;
	}

	private void InflateAndInvalidate (in RectangleI passedRectangle)
	{
		//Create a new instance to preserve the passed Rectangle.
		RectangleI r = new (
			passedRectangle.Location,
			passedRectangle.Size);

		r = r.Inflated (2, 2);

		workspace.Invalidate (r);
	}

	#endregion

	#region Undo/Redo

	protected override bool OnHandleUndo (Document document)
	{
		if (!is_editing)
			return false;

		// commit a history item to let the undo action undo text history item
		StopEditing (false);

		return false;
	}

	protected override bool OnHandleRedo (Document document)
	{
		//Rather than redoing something, if the text has been edited then simply commit and do not redo.
		if (!is_editing || CurrentTextEngine.State != TextMode.Uncommitted)
			return false;

		//Commit a new TextHistoryItem.
		StopEditing (false);

		return true;
	}

	#endregion

	#region Copy/Paste

	protected override async Task<bool> OnHandlePaste (Document document, IClipboardService cb)
	{
		if (!is_editing)
			return false;

		if (!await CurrentTextEngine.PerformPaste (cb))
			return false;

		RedrawText (true, true);
		return true;
	}

	protected override bool OnHandleCopy (Document document, IClipboardService cb)
	{
		if (!is_editing)
			return false;

		CurrentTextEngine.PerformCopy (cb);
		return true;
	}

	protected override bool OnHandleCut (Document document, IClipboardService cb)
	{
		if (!is_editing)
			return false;

		CurrentTextEngine.PerformCut (cb);
		RedrawText (true, true);
		return true;
	}

	#endregion
}
