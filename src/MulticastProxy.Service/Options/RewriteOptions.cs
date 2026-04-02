namespace MulticastProxy.Service.Options;

public sealed class RewriteOptions
{
    public const string SectionName = "Rewrite";

    public string? PayloadRewriteSourceSubnet { get; set; }
    public string? PayloadRewriteDestinationSubnet { get; set; }
}
