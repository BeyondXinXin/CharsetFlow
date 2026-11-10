namespace CharsetFlow.UI;

internal static class Theme
{
    public static readonly Color Window = Color.White;
    public static readonly Color Subtle = Color.FromArgb(245, 247, 250);
    public static readonly Color Border = Color.FromArgb(222, 227, 233);
    public static readonly Color Text = Color.FromArgb(26, 34, 46);
    public static readonly Color Muted = Color.FromArgb(76, 87, 103);
    public static readonly Color Accent = Color.FromArgb(0, 90, 158);
    public static readonly Color AccentLight = Color.FromArgb(226, 241, 252);
    public static readonly Color Success = Color.FromArgb(16, 124, 16);
    public static readonly Color Warning = Color.FromArgb(157, 93, 0);
    public static readonly Color Danger = Color.FromArgb(177, 36, 25);

    public static Button CreateButton(string text, bool primary = false)
    {
        Button button = new()
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 4, 10, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Window,
            ForeColor = primary ? Color.White : Text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 75, 135) : Subtle;
        return button;
    }
}
