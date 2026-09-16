using System.Drawing;
using System.Drawing.Text;

namespace SmartSwitcher.UI;

public static class ThemeFonts
{
    private static readonly PrivateFontCollection _fontCollection = new();
    private static readonly FontFamily _interFamily;
    private static readonly bool _isLoaded;

    static ThemeFonts()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fontsDir = Path.Combine(baseDir, "Resources", "Fonts");

            string[] fontFiles =
            [
                Path.Combine(fontsDir, "Inter-Regular.ttf"),
                Path.Combine(fontsDir, "Inter-Medium.ttf"),
                Path.Combine(fontsDir, "Inter-SemiBold.ttf"),
                Path.Combine(fontsDir, "Inter-Bold.ttf")
            ];

            foreach (var fontFile in fontFiles)
            {
                if (File.Exists(fontFile))
                {
                    _fontCollection.AddFontFile(fontFile);
                    _isLoaded = true;
                }
            }

            if (_isLoaded && _fontCollection.Families.Length > 0)
            {
                _interFamily = _fontCollection.Families[0];
            }
            else
            {
                _interFamily = TryGetSystemFontFamily("Inter", "Segoe UI Variable Text", "Segoe UI");
            }
        }
        catch
        {
            _interFamily = TryGetSystemFontFamily("Segoe UI", "Tahoma");
        }
    }

    private static FontFamily TryGetSystemFontFamily(params string[] familyNames)
    {
        foreach (var name in familyNames)
        {
            try
            {
                using var testFont = new Font(name, 9f);
                if (testFont.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return new FontFamily(name);
                }
            }
            catch
            {
                // Continue to next candidate
            }
        }
        return FontFamily.GenericSansSerif;
    }

    public static FontFamily FontFamily => _interFamily;

    public static Font Create(float emSize, FontStyle style = FontStyle.Regular)
    {
        try
        {
            return new Font(_interFamily, emSize, style, GraphicsUnit.Point);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, emSize, style, GraphicsUnit.Point);
        }
    }

    // Standard Semantic Typography
    public static Font TitleLarge => Create(13.5f, FontStyle.Bold);
    public static Font Subhead => Create(7.5f, FontStyle.Bold);
    public static Font StatusBanner => Create(9.5f, FontStyle.Bold);
    public static Font CardTitle => Create(10.5f, FontStyle.Bold);
    public static Font Badge => Create(7.5f, FontStyle.Bold);
    public static Font ScoreDisplay => Create(24.0f, FontStyle.Bold);
    public static Font ScoreMax => Create(9.0f, FontStyle.Regular);
    public static Font MetricValue => Create(11.5f, FontStyle.Bold);
    public static Font MetricLabel => Create(8.0f, FontStyle.Regular);
    public static Font Body => Create(9.0f, FontStyle.Regular);
    public static Font BodyMedium => Create(9.0f, FontStyle.Bold);
    public static Font ButtonText => Create(8.8f, FontStyle.Bold);
    public static Font LogText => Create(8.5f, FontStyle.Regular);
}
