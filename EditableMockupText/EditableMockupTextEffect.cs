using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;

namespace EditableMockupText;

public sealed class EditableMockupTextEffect : PropertyBasedEffect
{
    private const string PropertyNameText = "Text";
    private const string PropertyNameX = "X";
    private const string PropertyNameY = "Y";
    private const string PropertyNameFontFamily = "FontFamily";
    private const string PropertyNameFontSize = "FontSize";
    private const string PropertyNameBold = "Bold";
    private const string PropertyNameItalic = "Italic";
    private const string PropertyNameColor = "TextColor";
    private const string PropertyNameCharSpacing = "CharSpacing";
    private const string PropertyNameLineSpacing = "LineSpacing";
    private const string PropertyNameLayerAlias = "LayerAlias";
    private const string PropertyNameRebuildLayer = "RebuildLayer";
    private const string PropertyNameJsonPath = "JsonPath";

    private string text = "Sample text";
    private int x;
    private int y;
    private string fontFamily = "Segoe UI";
    private double fontSize = 42;
    private bool bold;
    private bool italic;
    private ColorBgra textColor = ColorBgra.Black;
    private double charSpacing;
    private double lineSpacing = 1.2;
    private string layerAlias = string.Empty;
    private bool rebuildLayer = true;
    private string jsonPath = string.Empty;
    private string lastLoadKey = string.Empty;

    public EditableMockupTextEffect()
        : base(
            "Editable Mockup Text",
            (Image?)null,
            "Text",
            new EffectOptions
            {
                Flags = EffectFlags.Configurable
            })
    {
    }

    protected override PropertyCollection OnCreatePropertyCollection()
    {
        string[] installedFonts = GetInstalledFontFamilies();
        int defaultFontIndex = Array.FindIndex(installedFonts, s => s.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase));
        if (defaultFontIndex < 0)
        {
            defaultFontIndex = 0;
        }

        return new PropertyCollection(
        [
            new StringProperty(PropertyNameText, "Sample text"),
            new Int32Property(PropertyNameX, 60, -32000, 32000),
            new Int32Property(PropertyNameY, 60, -32000, 32000),
            new StaticListChoiceProperty(PropertyNameFontFamily, installedFonts, defaultFontIndex),
            new DoubleProperty(PropertyNameFontSize, 42, 6, 400),
            new BooleanProperty(PropertyNameBold, false),
            new BooleanProperty(PropertyNameItalic, false),
            new StringProperty(PropertyNameLayerAlias, string.Empty),
            new StringProperty(PropertyNameJsonPath, string.Empty),
            new DoubleProperty(PropertyNameCharSpacing, 0, -20, 60),
            new DoubleProperty(PropertyNameLineSpacing, 1.2, 0.3, 5),
            new BooleanProperty(PropertyNameRebuildLayer, true),
            new Int32Property(PropertyNameColor, unchecked((int)ColorBgra.Black.Bgra))
        ]);
    }

    protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
    {
        ControlInfo configUi = CreateDefaultConfigUI(props);

        configUi.SetPropertyControlValue(PropertyNameText, ControlInfoPropertyNames.DisplayName, "Text");
        configUi.SetPropertyControlValue(PropertyNameX, ControlInfoPropertyNames.DisplayName, "X");
        configUi.SetPropertyControlValue(PropertyNameY, ControlInfoPropertyNames.DisplayName, "Y");
        configUi.SetPropertyControlValue(PropertyNameFontFamily, ControlInfoPropertyNames.DisplayName, "Font family");
        configUi.SetPropertyControlValue(PropertyNameFontSize, ControlInfoPropertyNames.DisplayName, "Font size");
        configUi.SetPropertyControlValue(PropertyNameBold, ControlInfoPropertyNames.DisplayName, "Bold");
        configUi.SetPropertyControlValue(PropertyNameItalic, ControlInfoPropertyNames.DisplayName, "Italic");
        configUi.SetPropertyControlValue(PropertyNameColor, ControlInfoPropertyNames.DisplayName, "Text color");
        configUi.SetPropertyControlValue(PropertyNameLayerAlias, ControlInfoPropertyNames.DisplayName, "Layer alias");
        configUi.SetPropertyControlValue(PropertyNameLayerAlias, ControlInfoPropertyNames.Description, "Unique name per text layer (e.g. title, subtitle).");
        configUi.SetPropertyControlValue(PropertyNameCharSpacing, ControlInfoPropertyNames.DisplayName, "Letter spacing");
        configUi.SetPropertyControlValue(PropertyNameLineSpacing, ControlInfoPropertyNames.DisplayName, "Line spacing");
        configUi.SetPropertyControlValue(PropertyNameRebuildLayer, ControlInfoPropertyNames.DisplayName, "Rebuild text layer");
        configUi.SetPropertyControlValue(PropertyNameJsonPath, ControlInfoPropertyNames.DisplayName, "JSON path");

        configUi.SetPropertyControlValue(PropertyNameText, ControlInfoPropertyNames.Multiline, true);
        configUi.SetPropertyControlType(PropertyNameFontFamily, PropertyControlType.DropDown);
        configUi.SetPropertyControlType(PropertyNameColor, PropertyControlType.ColorWheel);
        configUi.SetPropertyControlType(PropertyNameJsonPath, PropertyControlType.FileChooser);
        configUi.SetPropertyControlValue(PropertyNameJsonPath, ControlInfoPropertyNames.AllowAllFiles, true);

        return configUi;
    }

    protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken? newToken, RenderArgs dstArgs, RenderArgs srcArgs)
    {
        base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        if (newToken is null)
        {
            return;
        }

        layerAlias = newToken.GetProperty<StringProperty>(PropertyNameLayerAlias).Value.Trim();
        jsonPath = newToken.GetProperty<StringProperty>(PropertyNameJsonPath).Value.Trim();

        if (string.IsNullOrWhiteSpace(jsonPath) && !string.IsNullOrWhiteSpace(layerAlias))
        {
            jsonPath = MockupJsonLinkStore.GetJsonPathForAlias(layerAlias)
                ?? MockupJsonLinkStore.GetDefaultJsonPathForAlias(layerAlias);
        }

        rebuildLayer = newToken.GetProperty<BooleanProperty>(PropertyNameRebuildLayer).Value;

        string loadKey = $"{layerAlias}|{jsonPath}";
        bool layerContextChanged = !string.Equals(lastLoadKey, loadKey, StringComparison.OrdinalIgnoreCase);
        lastLoadKey = loadKey;

        if (layerContextChanged
            && !string.IsNullOrWhiteSpace(jsonPath)
            && File.Exists(jsonPath))
        {
            TryLoadFromJson(jsonPath);
        }
        else
        {
            ApplyTokenTextProperties(newToken);
        }

        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            TrySaveToJson(jsonPath);
            if (!string.IsNullOrWhiteSpace(layerAlias))
            {
                MockupJsonLinkStore.SetJsonPathForAlias(layerAlias, jsonPath);
            }
        }
    }

    protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
    {
        Surface dst = DstArgs.Surface;
        Surface src = SrcArgs.Surface;

        for (int i = startIndex; i < startIndex + length; i++)
        {
            Rectangle rect = rois[i];
            if (rebuildLayer)
            {
                ClearRectangleToTransparent(dst, rect);
            }
            else
            {
                dst.CopySurface(src, rect.Location, rect);
            }
        }

        using Bitmap bitmap = dst.CreateAliasedBitmap();
        using Graphics g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        FontStyle style = FontStyle.Regular;
        if (bold)
        {
            style |= FontStyle.Bold;
        }

        if (italic)
        {
            style |= FontStyle.Italic;
        }

        using Font font = new(fontFamily, (float)fontSize, style, GraphicsUnit.Pixel);
        using SolidBrush brush = new(Color.FromArgb(textColor.A, textColor.R, textColor.G, textColor.B));

        DrawMultilineTextWithoutWrap(g, font, brush, x, y);
    }

    private void DrawMultilineTextWithoutWrap(Graphics g, Font font, SolidBrush brush, float startX, float startY)
    {
        string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        float yPos = startY;

        foreach (string line in lines)
        {
            DrawLineWithCustomLetterSpacing(g, font, brush, line, startX, yPos);

            float lineHeight = font.GetHeight(g);
            yPos += lineHeight * (float)lineSpacing;
        }
    }

    private void DrawLineWithCustomLetterSpacing(Graphics g, Font font, SolidBrush brush, string line, float startX, float yPos)
    {
        using StringFormat measureFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
        measureFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        float xPos = startX;

        foreach (char ch in line)
        {
            string s = ch.ToString();
            g.DrawString(s, font, brush, xPos, yPos, StringFormat.GenericTypographic);

            SizeF charSize = g.MeasureString(s, font, PointF.Empty, measureFormat);
            xPos += charSize.Width + (float)charSpacing;
        }
    }

    private void ApplyTokenTextProperties(PropertyBasedEffectConfigToken token)
    {
        text = token.GetProperty<StringProperty>(PropertyNameText).Value;
        x = token.GetProperty<Int32Property>(PropertyNameX).Value;
        y = token.GetProperty<Int32Property>(PropertyNameY).Value;
        fontFamily = (string)token.GetProperty<StaticListChoiceProperty>(PropertyNameFontFamily).Value;
        fontSize = token.GetProperty<DoubleProperty>(PropertyNameFontSize).Value;
        bold = token.GetProperty<BooleanProperty>(PropertyNameBold).Value;
        italic = token.GetProperty<BooleanProperty>(PropertyNameItalic).Value;
        textColor = ColorBgra.FromUInt32(unchecked((uint)token.GetProperty<Int32Property>(PropertyNameColor).Value));
        charSpacing = token.GetProperty<DoubleProperty>(PropertyNameCharSpacing).Value;
        lineSpacing = token.GetProperty<DoubleProperty>(PropertyNameLineSpacing).Value;
    }

    private void TryLoadFromJson(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            string raw = File.ReadAllText(path);
            TextSettings? settings = JsonSerializer.Deserialize<TextSettings>(raw);
            if (settings is null)
            {
                return;
            }

            text = settings.Text ?? text;
            x = settings.X;
            y = settings.Y;
            fontFamily = string.IsNullOrWhiteSpace(settings.FontFamily) ? fontFamily : settings.FontFamily;
            fontSize = settings.FontSize <= 0 ? fontSize : settings.FontSize;
            bold = settings.Bold;
            italic = settings.Italic;
            charSpacing = settings.CharSpacing;
            lineSpacing = settings.LineSpacing <= 0 ? lineSpacing : settings.LineSpacing;
            textColor = ColorBgra.FromBgra(settings.B, settings.G, settings.R, settings.A);
            layerAlias = string.IsNullOrWhiteSpace(settings.LayerAlias) ? layerAlias : settings.LayerAlias.Trim();
        }
        catch
        {
            // Keep editing session resilient: ignore malformed JSON and continue rendering.
        }
    }

    private void TrySaveToJson(string path)
    {
        try
        {
            TextSettings settings = new()
            {
                Text = text,
                X = x,
                Y = y,
                FontFamily = fontFamily,
                FontSize = fontSize,
                Bold = bold,
                Italic = italic,
                CharSpacing = charSpacing,
                LineSpacing = lineSpacing,
                LayerAlias = layerAlias,
                A = textColor.A,
                R = textColor.R,
                G = textColor.G,
                B = textColor.B
            };

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore write errors to avoid breaking preview while user edits settings.
        }
    }

    private sealed class TextSettings
    {
        public string Text { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public string FontFamily { get; set; } = "Segoe UI";
        public double FontSize { get; set; } = 42;
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public double CharSpacing { get; set; }
        public double LineSpacing { get; set; } = 1.2;
        public string LayerAlias { get; set; } = string.Empty;
        public byte A { get; set; } = 255;
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }

    private static string[] GetInstalledFontFamilies()
    {
        using InstalledFontCollection fonts = new();
        string[] names = fonts.Families
            .Select(f => f.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (names.Length == 0)
        {
            return ["Segoe UI"];
        }

        return names;
    }

    private static void ClearRectangleToTransparent(Surface surface, Rectangle rect)
    {
        ColorBgra transparent = ColorBgra.FromBgra(0, 0, 0, 0);
        for (int y = rect.Top; y < rect.Bottom; y++)
        {
            for (int x = rect.Left; x < rect.Right; x++)
            {
                surface[x, y] = transparent;
            }
        }
    }

    private static class MockupJsonLinkStore
    {
        private static readonly object Sync = new();
        private static readonly string StoreDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EditableMockupText");
        private static readonly string AliasLinksPath = Path.Combine(StoreDirectory, "alias-links.json");

        public static string GetDefaultJsonPathForAlias(string alias)
        {
            string safeAlias = SanitizeFileName(alias);
            return Path.Combine(StoreDirectory, "blocks", safeAlias + ".pdntext.json");
        }

        public static string? GetJsonPathForAlias(string alias)
        {
            lock (Sync)
            {
                try
                {
                    Dictionary<string, string>? map = ReadAliasMap();
                    if (map is null)
                    {
                        return null;
                    }

                    return map.TryGetValue(alias, out string? path) ? path : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void SetJsonPathForAlias(string alias, string jsonPathValue)
        {
            lock (Sync)
            {
                try
                {
                    if (!Directory.Exists(StoreDirectory))
                    {
                        Directory.CreateDirectory(StoreDirectory);
                    }

                    Dictionary<string, string> map = ReadAliasMap() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    map[alias] = jsonPathValue;

                    string json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(AliasLinksPath, json);
                }
                catch
                {
                    // Ignore cache write failures.
                }
            }
        }

        private static Dictionary<string, string>? ReadAliasMap()
        {
            if (!File.Exists(AliasLinksPath))
            {
                return null;
            }

            string json = File.ReadAllText(AliasLinksPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new(name.Length);
            foreach (char ch in name)
            {
                sb.Append(invalid.Contains(ch) ? '_' : ch);
            }

            return sb.ToString();
        }
    }
}
