using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SmartSwitcher.UI;

/// <summary>
/// High-fidelity label rendering with subpixel ClearType antialiasing and zero pixelation.
/// </summary>
public class SmoothLabel : Label
{
    public SmoothLabel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.Top;
        if (TextAlign == ContentAlignment.MiddleCenter)
            flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        else if (TextAlign == ContentAlignment.MiddleRight)
            flags = TextFormatFlags.Right | TextFormatFlags.VerticalCenter;
        else if (TextAlign == ContentAlignment.MiddleLeft)
            flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter;

        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
    }
}

/// <summary>
/// Minimalist, sleek 6px progress bar with rounded ends and antialiased gradient fill.
/// Replaces clunky legacy 3D Windows progress bars.
/// </summary>
public class SmoothProgressBar : Control
{
    private int _value;
    private int _maximum = 100;
    private Color _trackColor = Color.FromArgb(24, 32, 42);
    private Color _fillColor = Color.FromArgb(69, 195, 252);
    private Color _gradientEndColor = Color.FromArgb(21, 140, 242);

    public SmoothProgressBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        Height = 6;
    }

    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, _maximum);
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(1, value);
            Invalidate();
        }
    }

    public Color TrackColor
    {
        get => _trackColor;
        set { _trackColor = value; Invalidate(); }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    public Color GradientEndColor
    {
        get => _gradientEndColor;
        set { _gradientEndColor = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int radius = Height / 2;
        var trackRect = new Rectangle(0, 0, Width, Height);

        // Draw track
        using var trackPath = CreateRoundedRectPath(trackRect, radius);
        using var trackBrush = new SolidBrush(_trackColor);
        g.FillPath(trackBrush, trackPath);

        // Draw progress fill
        if (_value > 0 && Width > 0)
        {
            int fillWidth = Math.Max(Height, (int)((float)_value / _maximum * Width));
            fillWidth = Math.Min(Width, fillWidth);
            var fillRect = new Rectangle(0, 0, fillWidth, Height);

            using var fillPath = CreateRoundedRectPath(fillRect, radius);
            using var fillBrush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(Width, 0),
                _fillColor,
                _gradientEndColor);
            g.FillPath(fillBrush, fillPath);
        }
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (rect.Width <= radius * 2 || rect.Height <= radius * 2)
        {
            path.AddRectangle(rect);
            return path;
        }

        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Modern flat button with 6px rounded corners, crisp hover transitions, and antialiased typography.
/// </summary>
public class ModernButton : Button
{
    private bool _isHovered;
    private bool _isSelected;
    private Color _baseBackColor = Color.FromArgb(24, 33, 44);
    private Color _hoverBackColor = Color.FromArgb(34, 46, 62);
    private Color _selectedBackColor = Color.FromArgb(17, 34, 56);
    private Color _borderColor = Color.FromArgb(46, 58, 72);
    private Color _selectedBorderColor = Color.FromArgb(69, 195, 252);
    private Color _selectedForeColor = Color.FromArgb(246, 252, 254);
    private Color _normalForeColor = Color.FromArgb(180, 192, 204);

    public ModernButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = ThemeFonts.ButtonText;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            Invalidate();
        }
    }

    public Color BaseBackColor
    {
        get => _baseBackColor;
        set { _baseBackColor = value; Invalidate(); }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; Invalidate(); }
    }

    public Color SelectedBorderColor
    {
        get => _selectedBorderColor;
        set { _selectedBorderColor = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = 6;

        Color currentBg = _isSelected
            ? _selectedBackColor
            : (_isHovered ? _hoverBackColor : _baseBackColor);

        Color currentBorder = _isSelected
            ? _selectedBorderColor
            : (_isHovered ? Color.FromArgb(69, 195, 252) : _borderColor);

        Color currentText = _isSelected ? _selectedForeColor : (_isHovered ? Color.White : _normalForeColor);

        using var path = CreateRoundedRectPath(rect, radius);
        using var bgBrush = new SolidBrush(currentBg);
        g.FillPath(bgBrush, path);

        using var borderPen = new Pen(currentBorder, _isSelected ? 1.5f : 1.0f);
        g.DrawPath(borderPen, path);

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            ClientRectangle,
            currentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
