using System.Text.Json;
using System.Threading.Channels;
using System.Reflection;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Debugging;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Services;

public sealed class DebugEventSink : BackgroundService, IDebugEventSink
{
    private const string ActivePathMarkerFileName = "debug-events.current.txt";

    private readonly Channel<RelayDebugEvent> _channel = Channel.CreateBounded<RelayDebugEvent>(new BoundedChannelOptions(5000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    private readonly DebugWindowOptions _options;
    private readonly RelayOptions _relayOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DebugEventSink> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public DebugEventSink(
        IOptions<DebugWindowOptions> options,
        IOptions<RelayOptions> relayOptions,
        IHostEnvironment environment,
        ILogger<DebugEventSink> logger)
    {
        _options = options.Value;
        _relayOptions = relayOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public void PublishPacket(
        string stage,
        Guid traceId,
        int port,
        byte[] payload,
        string? remoteEndpoint = null,
        string? details = null,
        byte[]? rewrittenPayload = null)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var maxPreviewBytes = Math.Max(1, _options.MaxPayloadPreviewBytes);
        var debugEvent = new RelayDebugEvent(
            TimestampUtc: DateTimeOffset.UtcNow,
            Stage: stage,
            TraceId: traceId,
            Port: port,
            PayloadLength: payload.Length,
            RemoteEndpoint: remoteEndpoint,
            Details: details,
            HexPreview: DebugPayloadFormatter.CreateHexPreview(payload, maxPreviewBytes),
            TextPreview: DebugPayloadFormatter.CreateTextPreview(payload, maxPreviewBytes),
            RewrittenTextPreview: rewrittenPayload is null
                ? null
                : DebugPayloadFormatter.CreateTextPreview(rewrittenPayload, maxPreviewBytes));

        _channel.Writer.TryWrite(debugEvent);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Debug window event sink disabled.");
            return;
        }

        var maxFileBytes = Math.Max(1, _options.MaxFileSizeMegabytes) * 1024L * 1024L;

        try
        {
            if (!TryOpenWriterWithFallback(out var stream, out var writer, out var eventsFilePath))
            {
                _logger.LogError("Debug event sink could not open any writable output path.");
                return;
            }

            TryWriteActivePathMarkers(eventsFilePath);
            _logger.LogInformation("Writing debug window events to {EventsFilePath}.", eventsFilePath);
            await WriteStartupEventAsync(writer, eventsFilePath, stoppingToken);

            await foreach (var debugEvent in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                if (stream.Length >= maxFileBytes)
                {
                    writer.Dispose();
                    stream.Dispose();
                    Rotate(eventsFilePath);
                    (stream, writer) = OpenWriter(eventsFilePath);
                }

                var json = JsonSerializer.Serialize(debugEvent, _serializerOptions);
                await writer.WriteLineAsync(json);
                await writer.FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug event sink stopped unexpectedly.");
        }
    }

    private string ResolveEventsFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_options.EventsFilePath))
        {
            return Environment.ExpandEnvironmentVariables(_options.EventsFilePath);
        }

        return GetDefaultProgramDataPath();
    }

    private async Task WriteStartupEventAsync(StreamWriter writer, string eventsFilePath, CancellationToken cancellationToken)
    {
        var assembly = typeof(DebugEventSink).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? "(not set)";
        var moduleVersionId = assembly.ManifestModule.ModuleVersionId;
        var assemblyLocation = assembly.Location;
        var assemblyLastWriteUtc = string.IsNullOrWhiteSpace(assemblyLocation) || !File.Exists(assemblyLocation)
            ? "(unknown)"
            : File.GetLastWriteTimeUtc(assemblyLocation).ToString("O");
        var configuredAppSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
        var ports = _relayOptions.MulticastPorts.Count == 0
            ? "(none)"
            : string.Join(',', _relayOptions.MulticastPorts);

        var startupEvent = new RelayDebugEvent(
            TimestampUtc: DateTimeOffset.UtcNow,
            Stage: "ServiceStarted",
            TraceId: Guid.Empty,
            Port: _relayOptions.TunnelPort,
            PayloadLength: 0,
            RemoteEndpoint: null,
            Details: string.Join(
                Environment.NewLine,
                [
                    $"Environment: {_environment.EnvironmentName}",
                    $"ContentRoot: {_environment.ContentRootPath}",
                    $"BaseDirectory: {AppContext.BaseDirectory}",
                    $"CurrentDirectory: {Environment.CurrentDirectory}",
                    $"ServiceVersion: {typeof(DebugEventSink).Assembly.GetName().Version}",
                    $"ServiceInformationalVersion: {informationalVersion}",
                    $"ServiceModuleVersionId: {moduleVersionId}",
                    $"ServiceAssemblyPath: {assemblyLocation}",
                    $"ServiceAssemblyLastWriteUtc: {assemblyLastWriteUtc}",
                    $"AppSettingsPath: {configuredAppSettingsPath}",
                    $"AppSettingsExists: {File.Exists(configuredAppSettingsPath)}",
                    $"DebugWindowEnabled: {_options.Enabled}",
                    $"DebugWindowConfiguredPath: {_options.EventsFilePath ?? "(default)"}",
                    $"DebugEventsFile: {eventsFilePath}",
                    $"MulticastGroup: {_relayOptions.MulticastGroup}",
                    $"MulticastPorts: {ports}",
                    $"TunnelPort: {_relayOptions.TunnelPort}",
                    $"DestinationIP: {_relayOptions.DestinationIP}",
                    $"LoopbackSuppressionWindowSeconds: {_relayOptions.LoopbackSuppressionWindowSeconds}",
                    $"InstanceId: {_relayOptions.InstanceId}"
                ]),
            HexPreview: string.Empty,
            TextPreview: "Service startup diagnostics",
            RewrittenTextPreview: null);

        var json = JsonSerializer.Serialize(startupEvent, _serializerOptions);
        await writer.WriteLineAsync(json);
        await writer.FlushAsync(cancellationToken);
    }

    private void TryWriteActivePathMarkers(string eventsFilePath)
    {
        foreach (var markerPath in GetActivePathMarkerPaths(eventsFilePath))
        {
            try
            {
                var directory = Path.GetDirectoryName(markerPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                Directory.CreateDirectory(directory);
                File.WriteAllText(markerPath, eventsFilePath + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to write debug path marker file at {MarkerPath}.", markerPath);
            }
        }
    }

    private IEnumerable<string> GetActivePathMarkerPaths(string eventsFilePath)
    {
        var localMarkerPath = Path.Combine(Path.GetDirectoryName(eventsFilePath)!, ActivePathMarkerFileName);
        yield return localMarkerPath;

        var defaultMarkerPath = GetDefaultPathMarkerPath();
        if (!string.Equals(localMarkerPath, defaultMarkerPath, StringComparison.OrdinalIgnoreCase))
        {
            yield return defaultMarkerPath;
        }
    }

    private bool TryOpenWriterWithFallback(out FileStream stream, out StreamWriter writer, out string eventsFilePath)
    {
        foreach (var candidate in GetCandidatePaths())
        {
            try
            {
                var directory = Path.GetDirectoryName(candidate);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                Directory.CreateDirectory(directory);
                (stream, writer) = OpenWriter(candidate);
                eventsFilePath = candidate;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to open debug event file at {EventsFilePath}. Trying next fallback path.", candidate);
            }
        }

        stream = null!;
        writer = null!;
        eventsFilePath = null!;
        return false;
    }

    private IEnumerable<string> GetCandidatePaths()
    {
        var primaryPath = ResolveEventsFilePath();
        yield return primaryPath;

        var defaultProgramDataPath = GetDefaultProgramDataPath();
        if (!string.Equals(primaryPath, defaultProgramDataPath, StringComparison.OrdinalIgnoreCase))
        {
            yield return defaultProgramDataPath;
        }

        yield return Path.Combine(Path.GetTempPath(), "MulticastProxy", "debug-events.jsonl");
    }

    private static string GetDefaultProgramDataPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "MulticastProxy",
                "debug-events.jsonl");
        }

        return Path.Combine(AppContext.BaseDirectory, "debug-events.jsonl");
    }

    private static string GetDefaultPathMarkerPath() =>
        Path.Combine(Path.GetDirectoryName(GetDefaultProgramDataPath())!, ActivePathMarkerFileName);

    private static (FileStream Stream, StreamWriter Writer) OpenWriter(string path)
    {
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true);
        var writer = new StreamWriter(stream);
        return (stream, writer);
    }

    private void Rotate(string path)
    {
        try
        {
            var previousPath = $"{path}.previous";
            if (File.Exists(previousPath))
            {
                File.Delete(previousPath);
            }

            if (File.Exists(path))
            {
                File.Move(path, previousPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rotate debug event file {EventsFilePath}. Continuing to append.", path);
        }
    }
}
