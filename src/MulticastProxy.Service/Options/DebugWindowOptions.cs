namespace MulticastProxy.Service.Options;

public sealed class DebugWindowOptions
{
    public const string SectionName = "DebugWindow";

    public bool Enabled { get; set; } = true;
    public string? EventsFilePath { get; set; }
    public int MaxPayloadPreviewBytes { get; set; } = 256;
    public int MaxFileSizeMegabytes { get; set; } = 25;
}
