using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Services;

namespace MulticastProxy.Service.Tests;

public class DebugEventSinkTests
{
    [Fact]
    public async Task StartAsync_WhenDebugWindowEnabled_WritesStartupEventFile()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "MulticastProxy.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);

        try
        {
            var eventsFilePath = Path.Combine(testDirectory, "debug-events.jsonl");
            var options = Microsoft.Extensions.Options.Options.Create(new DebugWindowOptions
            {
                Enabled = true,
                EventsFilePath = eventsFilePath
            });

            var relayOptions = Microsoft.Extensions.Options.Options.Create(new RelayOptions
            {
                MulticastGroup = "239.0.0.1",
                MulticastPorts = [9053],
                TunnelPort = 19053,
                DestinationIP = "198.51.100.10",
                InstanceId = Guid.NewGuid()
            });

            var environment = new TestHostEnvironment
            {
                ContentRootPath = testDirectory
            };

            using (var sink = new DebugEventSink(
                       options,
                       relayOptions,
                       environment,
                       NullLogger<DebugEventSink>.Instance))
            {
                await sink.StartAsync(CancellationToken.None);
                await WaitForFileAsync(eventsFilePath);
                await sink.StopAsync(CancellationToken.None);
            }

            var contents = await ReadAllTextWithSharedAccessAsync(eventsFilePath);
            Assert.Contains("\"stage\":\"ServiceStarted\"", contents);
            Assert.Contains("ServiceModuleVersionId:", contents);
            Assert.Contains("LoopbackSuppressionWindowSeconds:", contents);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static async Task WaitForFileAsync(string path)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"Expected debug event file at {path}.");
    }

    private static async Task<string> ReadAllTextWithSharedAccessAsync(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "MulticastProxy.Service.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
