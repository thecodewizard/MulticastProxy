using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Validation;

public sealed class DebugWindowOptionsPostConfigure : IPostConfigureOptions<DebugWindowOptions>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DebugWindowOptionsPostConfigure> _logger;

    public DebugWindowOptionsPostConfigure(
        IConfiguration configuration,
        ILogger<DebugWindowOptionsPostConfigure> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void PostConfigure(string? name, DebugWindowOptions options)
    {
        if (TryApplySectionValue(_configuration.GetSection(DebugWindowOptions.SectionName), options, "DebugWindow"))
        {
            return;
        }

        var legacySection = _configuration.GetSection(RelayOptions.SectionName).GetSection(DebugWindowOptions.SectionName);
        TryApplySectionValue(legacySection, options, "Relay:DebugWindow");
    }

    private bool TryApplySectionValue(IConfigurationSection section, DebugWindowOptions options, string sectionPath)
    {
        if (!section.Exists())
        {
            return false;
        }

        if (section.Value is null)
        {
            if (sectionPath == DebugWindowOptions.SectionName)
            {
                return true;
            }

            section.Bind(options);
            _logger.LogWarning(
                "{SectionPath} is deprecated. Move the DebugWindow settings to a top-level DebugWindow section.",
                sectionPath);
            return true;
        }

        if (!TryParseEnabled(section.Value, out var enabled))
        {
            _logger.LogWarning(
                "{SectionPath} value '{Value}' was not recognized. Use Enabled/Disabled or true/false.",
                sectionPath,
                section.Value);
            return true;
        }

        options.Enabled = enabled;
        if (sectionPath != DebugWindowOptions.SectionName)
        {
            _logger.LogWarning(
                "{SectionPath} is deprecated. Move the DebugWindow settings to a top-level DebugWindow section.",
                sectionPath);
        }

        return true;
    }

    private static bool TryParseEnabled(string value, out bool enabled)
    {
        if (bool.TryParse(value, out enabled))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "enabled":
            case "on":
            case "yes":
                enabled = true;
                return true;
            case "disabled":
            case "off":
            case "no":
                enabled = false;
                return true;
            default:
                enabled = false;
                return false;
        }
    }
}
