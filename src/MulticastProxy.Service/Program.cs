using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Protocol;
using MulticastProxy.Service.Services;
using MulticastProxy.Service.Validation;

var contentRoot = AppContext.BaseDirectory;
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = contentRoot
});

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

builder.Services
    .AddOptions<DebugWindowOptions>()
    .Bind(builder.Configuration.GetSection(DebugWindowOptions.SectionName));

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.AddSingleton<IValidateOptions<RelayOptions>, RelayOptionsValidator>();
builder.Services.AddSingleton<IPostConfigureOptions<RelayOptions>, RelayOptionsPostConfigure>();
builder.Services.AddSingleton<IPostConfigureOptions<DebugWindowOptions>, DebugWindowOptionsPostConfigure>();
builder.Services.AddSingleton<IValidateOptions<RewriteOptions>, RewriteOptionsValidator>();
builder.Services.AddSingleton<DebugEventSink>();
builder.Services.AddSingleton<RelayEnvelopeSerializer>();
builder.Services.AddSingleton<ITunnelSendQueue, TunnelSendQueue>();
builder.Services.AddSingleton<IMulticastEmitQueue, MulticastEmitQueue>();
builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();
builder.Services.AddSingleton<ILoopbackSuppressionService, LoopbackSuppressionService>();
builder.Services.AddSingleton<ILocalMulticastOriginService, LocalMulticastOriginService>();
builder.Services.AddSingleton<IPayloadRewriteService, PayloadRewriteService>();
builder.Services.AddSingleton<IDebugEventSink>(sp => sp.GetRequiredService<DebugEventSink>());
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DebugEventSink>());

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
