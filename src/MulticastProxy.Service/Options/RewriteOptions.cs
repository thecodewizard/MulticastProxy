namespace MulticastProxy.Service.Options;

public sealed class RewriteOptions
{
    public const string SectionName = "Rewrite";

    // Example: the scanner may announce 172.16.10.x on-site, while cloud users must see
    // 10.50.13.x so their follow-up TCP/ICMP/FTP traffic targets the NAT-visible address.
    public string? PayloadRewriteSourceSubnet { get; set; }
    public string? PayloadRewriteDestinationSubnet { get; set; }
}
