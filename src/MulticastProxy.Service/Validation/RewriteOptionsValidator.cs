using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Validation;

public sealed class RewriteOptionsValidator : IValidateOptions<RewriteOptions>
{
    public ValidateOptionsResult Validate(string? name, RewriteOptions options)
    {
        var hasSource = !string.IsNullOrWhiteSpace(options.PayloadRewriteSourceSubnet);
        var hasDestination = !string.IsNullOrWhiteSpace(options.PayloadRewriteDestinationSubnet);

        if (hasSource ^ hasDestination)
        {
            return ValidateOptionsResult.Fail(
                "Rewrite settings are invalid. Configure both Rewrite:PayloadRewriteSourceSubnet and Rewrite:PayloadRewriteDestinationSubnet, or neither.");
        }

        return ValidateOptionsResult.Success;
    }
}
