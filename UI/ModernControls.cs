using System.ComponentModel;

namespace CharsetFlow.UI;

internal sealed class SlimProgressBar : Control
{
    private int _maximum = 100;
    private int _value;

    public SlimProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 4;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(1, value);
            Value = Math.Min(Value, _maximum);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, Maximum);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using SolidBrush track = new(Theme.Border);
        using SolidBrush fill = new(Theme.Accent);
        e.Graphics.FillRectangle(track, ClientRectangle);
        int width = (int)Math.Round(Width * (Value / (double)Maximum));
        if (width > 0)
        {
            e.Graphics.FillRectangle(fill, new Rectangle(0, 0, width, Height));
        }
    }
}
