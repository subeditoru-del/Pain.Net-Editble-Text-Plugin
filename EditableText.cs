using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.Json;
using PaintDotNet;
using PaintDotNet.Effects;
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
    private const string PropertyNameJsonPath = "JsonPath";
    private const string PropertyNameJsonMode = "JsonMode";

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
    private string jsonPath = string.Empty;
    private JsonMode jsonMode = JsonMode.None;

    public EditableMockupTextEffect()
        : base("Editable Mockup Text", null, "Text", EffectFlags.Configurable)
    {
    }

    protected override PropertyCollection OnCreatePropertyCollection()
    {
        return new PropertyCollection(
        [
            new StringProperty(PropertyNameText, "Sample text"),
            new Int32Property(PropertyNameX, 60, -32000, 32000),
            new Int32Property(PropertyNameY, 60, -32000, 32000),
            new StringProperty(PropertyNameFontFamily, "Segoe UI"),
            new DoubleProperty(PropertyNameFontSize, 42, 6, 400),
            new BooleanProperty(PropertyNameBold, false),
            new BooleanProperty(PropertyNameItalic, false),
            StaticListChoiceProperty.CreateForEnum<JsonMode>(PropertyNameJsonMode, JsonMode.None, false),
            new StringProperty(PropertyNameJsonPath, string.Empty),
            new DoubleProperty(PropertyNameCharSpacing, 0, -20, 60),
            new DoubleProperty(PropertyNameLineSpacing, 1.2, 0.3, 5),
            new Int32Property(PropertyNameColor, ColorBgra.Black.Bgra)
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
        configUi.SetPropertyControlValue(PropertyNameCharSpacing, ControlInfoPropertyNames.DisplayName, "Letter spacing");
        configUi.SetPropertyControlValue(PropertyNameLineSpacing, ControlInfoPropertyNames.DisplayName, "Line spacing");
        configUi.SetPropertyControlValue(PropertyNameJsonPath, ControlInfoPropertyNames.DisplayName, "JSON path");
        configUi.SetPropertyControlValue(PropertyNameJsonMode, ControlInfoPropertyNames.DisplayName, "JSON action");

        configUi.SetPropertyControlValue(PropertyNameText, ControlInfoPropertyNames.Multiline, true);

        return configUi;
    }

    protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
    {
        base.OnSetRenderInfo(newToken, dstArgs, srcArgs);

        text = newToken.GetProperty<StringProperty>(PropertyNameText).Value;
        x = newToken.GetProperty<Int32Property>(PropertyNameX).Value;
        y = newToken.GetProperty<Int32Property>(PropertyNameY).Value;
        fontFamily = newToken.GetProperty<StringProperty>(PropertyNameFontFamily).Value;
        fontSize = newToken.GetProperty<DoubleProperty>(PropertyNameFontSize).Value;
        bold = newToken.GetProperty<BooleanProperty>(PropertyNameBold).Value;
        italic = newToken.GetProperty<BooleanProperty>(PropertyNameItalic).Value;
        textColor = ColorBgra.FromUInt32(unchecked((uint)newToken.GetProperty<Int32Property>(PropertyNameColor).Value));
        charSpacing = newToken.GetProperty<DoubleProperty>(PropertyNameCharSpacing).Value;
        lineSpacing = newToken.GetProperty<DoubleProperty>(PropertyNameLineSpacing).Value;
        jsonPath = newToken.GetProperty<StringProperty>(PropertyNameJsonPath).Value;
        jsonMode = (JsonMode)newToken.GetProperty<StaticListChoiceProperty>(PropertyNameJsonMode).Value;

        if (!string.IsNullOrWhiteSpace(jsonPath) && jsonMode == JsonMode.Load)
        {
            TryLoadFromJson(jsonPath);
        }

        if (!string.IsNullOrWhiteSpace(jsonPath) && jsonMode == JsonMode.Save)
        {
            TrySaveToJson(jsonPath);
        }
    }

    protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
    {
        Surface dst = DstArgs.Surface;
        Surface src = SrcArgs.Surface;

        for (int i = startIndex; i < startIndex + length; i++)
        {
            Rectangle rect = rois[i];
            dst.CopySurface(src, rect.Location, rect);
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
        float xPos = startX;

        foreach (char ch in line)
        {
            string s = ch.ToString();
            g.DrawString(s, font, brush, xPos, yPos, StringFormat.GenericTypographic);

            SizeF charSize = g.MeasureString(s, font, PointF.Empty, StringFormat.GenericTypographic);
            xPos += charSize.Width + (float)charSpacing;
        }
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
        public byte A { get; set; } = 255;
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }

    private enum JsonMode
    {
        None,
        Load,
        Save
    }
}
