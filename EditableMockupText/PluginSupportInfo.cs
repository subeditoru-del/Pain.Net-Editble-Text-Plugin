using System;
using PaintDotNet;

[assembly: PluginSupportInfo(typeof(EditableMockupText.PluginSupportInfo))]

namespace EditableMockupText;

public sealed class PluginSupportInfo : IPluginSupportInfo
{
    public string Author => "Anton";

    public string Copyright => "Copyright (c) 2026 Anton";

    public string DisplayName => "Editable Mockup Text";

    public Version Version => new(1, 0, 0, 0);

    public Uri WebsiteUri => new("https://forums.getpaint.net/");
}
