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
