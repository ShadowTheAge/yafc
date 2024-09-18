namespace Yafc.UI;

/// <summary>
/// Contains the display parameters for FactorioObjectIcons.
/// </summary>
/// <param name="Size">The icon size. The production tables use size 3.</param>
/// <param name="MilestoneDisplay">The <see cref="Yafc.MilestoneDisplay"/> option to use when drawing the icon.</param>
/// <param name="UseScaleSetting">Whether or not to obey the <see cref="Model.ProjectPreferences.iconScale"/> setting.</param>
public record IconDisplayStyle(float Size, MilestoneDisplay MilestoneDisplay, bool UseScaleSetting) {
    /// <summary>
    /// Gets the default icon style: Size 2, <see cref="MilestoneDisplay.Normal"/>, and not scaled.
    /// </summary>
    public static IconDisplayStyle Default { get; } = new(2, MilestoneDisplay.Normal, false);
}

/// <summary>
/// Contains the display parameters for FactorioObjectButtons. Buttons with a background color draw a scaled icon
/// (<c><see cref="IconDisplayStyle.UseScaleSetting"/> = <see langword="true"/></c>) by default.
/// </summary>
/// <param name="Size">The icon size. The production tables use size 3.</param>
/// <param name="MilestoneDisplay">The <see cref="MilestoneDisplay"/> option to use when drawing the icon.</param>
/// <param name="BackgroundColor">The background color to display behind the icon.</param>
public record ButtonDisplayStyle(float Size, MilestoneDisplay MilestoneDisplay, SchemeColor BackgroundColor) : IconDisplayStyle(Size, MilestoneDisplay, true) {
    /// <summary>
    /// Creates a new <see cref="ButtonDisplayStyle"/> for buttons that do not have a background color.
    /// These buttons will not obey the <see cref="Model.ProjectPreferences.iconScale"/> setting.
    /// </summary>
    /// <param name="size">The icon size. The production tables use size 3.</param>
    /// <param name="milestoneDisplay">The <see cref="MilestoneDisplay"/> option to use when drawing the icon.</param>
    public ButtonDisplayStyle(float size, MilestoneDisplay milestoneDisplay) : this(size, milestoneDisplay, SchemeColor.None) => UseScaleSetting = false;

    /// <summary>
    /// Gets the default button style: Size 2, <see cref="MilestoneDisplay.Normal"/>, no background, and not scaled.
    /// </summary>
    public static new ButtonDisplayStyle Default { get; } = new(2, MilestoneDisplay.Normal);
    /// <summary>
    /// Gets the button style for the <see cref="SelectObjectPanel{T}"/>s: Size 2.5, <see cref="MilestoneDisplay.Contained"/>, and scaled.
    /// </summary>
    /// <param name="backgroundColor">The background color to use for this button.</param>
    public static ButtonDisplayStyle SelectObjectPanel(SchemeColor backgroundColor) => new(2.5f, MilestoneDisplay.Contained, backgroundColor);
    /// <summary>
    /// Gets the button style for production table buttons with a background: Size 2.5, <see cref="MilestoneDisplay.Contained"/>, and scaled.
    /// </summary>
    /// <param name="backgroundColor">The background color to use for this button.</param>
    public static ButtonDisplayStyle ProductionTableScaled(SchemeColor backgroundColor) => new(3, MilestoneDisplay.Contained, backgroundColor);
    /// <summary>
    /// Gets the button style for production table buttons with no background: Size 2.5, <see cref="MilestoneDisplay.Contained"/>, and not scaled.
    /// </summary>
    public static ButtonDisplayStyle ProductionTableUnscaled { get; } = new(3, MilestoneDisplay.Contained);
    /// <summary>
    /// Gets the button style for small buttons in the Never Enough Items Explorer: Size 3, <see cref="MilestoneDisplay.Contained"/>, and not scaled.
    /// </summary>
    public static ButtonDisplayStyle NeieSmall { get; } = new(3, MilestoneDisplay.Contained);
    /// <summary>
    /// Gets the button style for large buttons in the Never Enough Items Explorer: Size 4, <see cref="MilestoneDisplay.Contained"/>, and not scaled.
    /// </summary>
    public static ButtonDisplayStyle NeieLarge { get; } = new(4, MilestoneDisplay.Contained);
    /// <summary>
    /// Gets the button style for the Milestones display buttons: Size 3, <see cref="MilestoneDisplay.None"/>, and scaled.
    /// </summary>
    /// <param name="backgroundColor">The background color to use for this button.</param>
    public static ButtonDisplayStyle Milestone(SchemeColor backgroundColor) => new(3, MilestoneDisplay.None, backgroundColor);
}
