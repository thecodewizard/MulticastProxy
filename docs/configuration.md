# Configuration

## Goal

This service must be configurable by operators with limited technical knowledge.

The primary configuration approach is:
- `appsettings.json` for normal deployments
- optional environment-specific overrides
- strongly validated settings at startup

When running under Windows Service Control Manager, the service resolves configuration from its executable directory. It does not rely on the process working directory, which is often `C:\Windows\System32` for services.

If configuration is invalid, the service should fail startup with a clear Windows Event Log message.

---

## Configuration model

The service is expected to support the following settings.

## Relay settings

### `Relay:MulticastGroup`
The multicast group address to listen on and re-emit to.

Example:

    "MulticastGroup": "239.0.0.1"

Notes:
- `239.0.0.0/8` is used here as an example administratively scoped multicast range for documentation.
- Must be a valid IPv4 multicast address if used.
- `224.0.0.0` is treated as wildcard mode. The service first attempts the same explicit join used by the legacy relay, then falls back to bind-only mode if the OS rejects that join.
- Do not hardcode this in source code.

---

### `Relay:MulticastPorts`
One or more UDP ports to listen for multicast traffic.

Example:

    "MulticastPorts": [9053]

Example with multiple ports:

    "MulticastPorts": [9053, 9100, 9999]

Rules:
- Each port must be between 0 and 65535.
- Empty lists should be rejected unless the implementation explicitly supports a disabled receive mode.

---

### `Relay:TunnelPort`
The UDP port used for unicast relay traffic between peers.

Example:

    "TunnelPort": 19053

Rules:
- Must be between 0 and 65535.
- The remote peer should normally listen on the same port unless future protocol logic supports port remapping.

---

### `Relay:DestinationIP`
The IP address of the remote relay peer.

Example:

    "DestinationIP": "198.51.100.10"

Rules:
- Must be a valid IP address.
- Hostnames may be supported in the future, but IP addresses are preferred for predictable service operation.

---

### `Relay:ListenInterfaceIP`
Optional local interface IP to use when joining the multicast group.

Example:

    "ListenInterfaceIP": "192.0.2.20"

When to use:
- systems with multiple NICs
- systems where multicast must be joined on a specific interface
- environments where automatic interface selection is unreliable

If omitted:
- the service may rely on default OS behavior

---

### `Relay:SendInterfaceIP`
Optional local interface IP to use when re-emitting multicast.

Example:

    "SendInterfaceIP": "198.51.100.20"

When to use:
- systems with multiple NICs
- systems where outbound multicast must leave through a specific interface

If omitted:
- the service may rely on default OS routing and interface behavior

---

### `Relay:LoopbackSuppressionWindowSeconds`
How long the service remembers a packet that it just re-emitted as multicast locally, so it does not tunnel that same packet again if the local multicast listener sees it.

Example:

    "LoopbackSuppressionWindowSeconds": 5

Rules:
- `0` disables loopback suppression
- positive values enable short-term suppression of locally re-emitted packets
- this setting is specifically intended to prevent ping-pong relay loops between two peers

---

### `Relay:InstanceId`
Optional unique relay identity used for loop prevention.

If omitted:
- the service generates a runtime GUID automatically
- this is the recommended default for most installs

If explicitly set:
- it must be unique per deployed node
- do not copy the same value to both peers or the receiver will treat the peer's packets as local and drop them

---

## Rewrite settings

Payload rewrite is optional.

If no rewrite settings are provided, payloads should be forwarded unchanged.

### `Rewrite:PayloadRewriteSourceSubnet`
The subnet string to search for inside supported payload text.

Example:

    "PayloadRewriteSourceSubnet": "192.0.2."

### `Rewrite:PayloadRewriteDestinationSubnet`
The replacement subnet string to write into supported payload text.

Example:

    "PayloadRewriteDestinationSubnet": "198.51.100."

Rules:
- If either rewrite setting is present, both must be present.
- If both are absent, rewrite is disabled.
- Rewrite should only occur when safe for the detected payload format.
- Binary or unknown payload formats should pass through unchanged.

---

## Debug window settings

The service can write a structured live event stream for the desktop debug viewer.
The file is created immediately at startup and begins with a `ServiceStarted` diagnostic event that records the effective content root and relay settings the service is using.

### `DebugWindow:Enabled`
Enables structured packet-flow tracing to the debug events file.

Recommended use:
- enabled by default so operators can immediately inspect live packet flow
- set it to `false` if you want to disable debug-event capture in a quieter deployment

### `DebugWindow:EventsFilePath`
Optional absolute path for the event file.

If omitted on Windows:
- the service writes to `%ProgramData%\MulticastProxy\debug-events.jsonl`

### `DebugWindow:MaxPayloadPreviewBytes`
Controls how many bytes of each payload are captured in the debug event preview.

Example:

    "MaxPayloadPreviewBytes": 256

### `DebugWindow:MaxFileSizeMegabytes`
Maximum size of the active debug event file before it rotates to a `.previous` file.

Example:

    "MaxFileSizeMegabytes": 25

---

## Logging settings

Use standard .NET logging configuration.

### `Logging:LogLevel:Default`
Controls overall logging level.

Recommended production value:

    "Default": "Information"

Recommended troubleshooting value:

    "Default": "Debug"

### Suggested logging policy
- `Information`: startup, shutdown, config validation, major state changes
- `Warning`: transient errors, malformed packets, recoverable failures
- `Error`: hard failures and startup-blocking issues
- `Debug`: per-packet summaries and rewrite details

Normal production deployments should not emit per-packet logs.

---

## Example `appsettings.json`

```json
{
  "Relay": {
    "MulticastGroup": "239.0.0.1",
    "MulticastPorts": [9053],
    "TunnelPort": 19053,
    "DestinationIP": "198.51.100.10",
    "ListenInterfaceIP": "192.0.2.20",
    "SendInterfaceIP": "198.51.100.20",
    "LoopbackSuppressionWindowSeconds": 5
  },
  "Rewrite": {
    "PayloadRewriteSourceSubnet": "192.0.2.",
    "PayloadRewriteDestinationSubnet": "198.51.100."
  },
  "DebugWindow": {
    "Enabled": true,
    "EventsFilePath": "",
    "MaxPayloadPreviewBytes": 256,
    "MaxFileSizeMegabytes": 25
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
