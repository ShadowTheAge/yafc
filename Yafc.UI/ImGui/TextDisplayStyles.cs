namespace Yafc.UI;

/// <summary>
/// Contains the display parameters for fixed text (<c>TextBlock</c> in WPF, <c>Label</c> in WinForms)
/// </summary>
/// <param name="Font">The <see cref="UI.Font"/> to use when drawing the text, or <see langword="null"/> to use <see cref="Font.text"/>.</param>
/// <param name="WrapText">Specifies whether or not the text should be wrapped.</param>
/// <param name="Alignment">Where the text should be drawn within the renderable area.</param>
/// <param name="Color">The color to use, or <see cref="SchemeColor.None"/> to use the previous color.</param>
public record TextBlockDisplayStyle(Font? Font = null, bool WrapText = false, RectAlignment Alignment = RectAlignment.MiddleLeft, SchemeColor Color = SchemeColor.None) {
    /// <summary>
    /// Gets the default display style (<see cref="Font.text"/>, not wrapped, left-aligned), with the specified color.
    /// </summary>
    /// <param name="color">The color to use, or <see cref="SchemeColor.None"/> to use the previous color.</param>
    public static TextBlockDisplayStyle Default(SchemeColor color = SchemeColor.None) => new(Color: color);
    /// <summary>
    /// Gets the display style for nonwrapped centered text.
    /// </summary>
    public static TextBlockDisplayStyle Centered { get; } = new(Alignment: RectAlignment.Middle);
    /// <summary>
    /// Gets the display style for hint text.
    /// </summary>
    public static TextBlockDisplayStyle HintText { get; } = new(Color: SchemeColor.BackgroundTextFaint);
    /// <summary>
    /// Gets the display style for wrapped, left-aligned text.
    /// </summary>
    public static TextBlockDisplayStyle WrappedText { get; } = new(WrapText: true);
    /// <summary>
    /// Gets the display style for most error messages.
    /// </summary>
    public static TextBlockDisplayStyle ErrorText { get; } = new(WrapText: true, Color: SchemeColor.Error);

    /// <summary>
    /// Converts a font to the default display style (not wrapped, left-aligned, default color) for that font.
    /// </summary>
    public static implicit operator TextBlockDisplayStyle(Font font) => new(font);
}

/// <summary>
/// Contains the display parameters for editable text (<c>TextBox</c> in both WPF and WinForms)
/// </summary>
/// <param name="Icon">The <see cref="Icon"/> to display to the left of the text, or <see cref="Icon.None"/> to display no icon.</param>
/// <param name="Padding">The <see cref="UI.Padding"/> to place between the text and the edges of the editable area. (The box area not used by <paramref name="Icon"/>.)</param>
/// <param name="Alignment">The <see cref="RectAlignment"/> to apply when drawing the text within the edit box.</param>
/// <param name="ColorGroup">The <see cref="SchemeColorGroup"/> to use when drawing the edit box.</param>
public record TextBoxDisplayStyle(Icon Icon, Padding Padding, RectAlignment Alignment, SchemeColorGroup ColorGroup) {
    /// <summary>
    /// Gets the default display style, used for the Preferences screen and calls to <see cref="ImGui.BuildTextInput(string?, out string, string?, Icon, bool, bool)"/>.
    /// </summary>
    public static TextBoxDisplayStyle DefaultTextInput { get; } = new(Icon.None, new Padding(.5f), RectAlignment.MiddleLeft, SchemeColorGroup.Grey);
    /// <summary>
    /// Gets the display style for input boxes on the Module Filler Parameters screen.
    /// </summary>
    public static TextBoxDisplayStyle ModuleParametersTextInput { get; } = new(Icon.None, new Padding(.5f, 0), RectAlignment.MiddleLeft, SchemeColorGroup.Grey);
    /// <summary>
    /// Gets the display style for amounts associated with Factorio objects. (<c><see langword="with"/> { ColorGroup = <see cref="SchemeColorGroup.Grey"/> }</c> for built building counts.)
    /// </summary>
    public static TextBoxDisplayStyle FactorioObjectInput { get; } = new(Icon.None, default, RectAlignment.Middle, SchemeColorGroup.Secondary);
}
