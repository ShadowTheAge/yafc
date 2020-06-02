namespace YAFC.UI
{
    public enum SchemeColor
    {
        // Special colors
        None,
        TextSelection,
        Link,
        Green,
        // Pure colors
        PureBackground, // White with light theme, black with dark
        PureForeground, // Black with light theme, white with dark
        Source, // Always white
        SourceFaint,
        // Background group
        Background,
        BackgroundAlt,
        BackgroundText,
        BackgroundTextFaint,
        // Primary group
        Primary,
        PrimalyAlt,
        PrimaryText,
        PrimaryTextFaint,
        // Secondary group
        Secondary,
        SecondaryAlt,
        SecondaryText,
        SecondaryTextFaint,
        // Error group
        Error,
        ErrorAlt,
        ErrorText,
        ErrorTextFaint,
        // Grey group
        Grey,
        GreyAlt,
        GreyText,
        GreyTextFaint,
    }

    public enum RectangleBorder
    {
        None,
        Thin,
        Full
    }

    public enum Icon
    {
        None,
        Check,
        CheckBoxCheck,
        CheckBoxEmpty,
        Close,
        Edit,
        Folder,
        FolderOpen,
        Help,
        NewFolder,
        Plus,
        Refresh,
        Search,
        Settings,
        Time,
        Upload,
        RadioEmpty,
        RadioCheck,
        Error,
        Warning,
        Add,
        DropDown,
        ShevronDown,
        ShevronUp,
        ShevronRight,
        Menu,
        ArrowRight,
        ArrowDownRight,
        Empty,
        DarkMode,
        FirstCustom
    }

    public enum RectAlignment
    {
        Full,
        MiddleLeft,
        Middle,
        MiddleFullRow,
        MiddleRight,
        UpperCenter
    }
    
    public readonly struct Padding
    {
        public readonly float left, right, top, bottom;

        public Padding(float allOffsets)
        {
            top = bottom = left = right = allOffsets;
        }

        public Padding(float leftRight, float topBottom)
        {
            left = right = leftRight;
            top = bottom = topBottom;
        }

        public Padding(float left, float right, float top, float bottom)
        {
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
        }
    }
}