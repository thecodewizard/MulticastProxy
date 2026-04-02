using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Protocol;
using MulticastProxy.Service.Services;
using MulticastProxy.Service.Validation;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MulticastProxy";
});

ConfigureWindowsEventLog(builder);

builder.Services
    .AddOptions<RelayOptions>()
    .Bind(builder.Configuration.GetSection(RelayOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<RewriteOptions>()
    .Bind(builder.Configuration.GetSection(RewriteOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<RelayOptions>, RelayOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<RewriteOptions>, RewriteOptionsValidator>();

builder.Services.AddSingleton<RelayEnvelopeSerializer>();
builder.Services.AddSingleton<ITunnelSendQueue, TunnelSendQueue>();
builder.Services.AddSingleton<IMulticastEmitQueue, MulticastEmitQueue>();
builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();
builder.Services.AddSingleton<IPayloadRewriteService, PayloadRewriteService>();

builder.Services.AddHostedService<MulticastReceiverService>();
builder.Services.AddHostedService<TunnelSenderService>();
builder.Services.AddHostedService<TunnelReceiverService>();
builder.Services.AddHostedService<MulticastEmitterService>();

await builder.Build().RunAsync();

static void ConfigureWindowsEventLog(HostApplicationBuilder builder)
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    builder.Logging.AddEventLog(settings =>
    {
#pragma warning disable CA1416
        settings.SourceName = "MulticastProxy";
#pragma warning restore CA1416
    });
}
