# Configuration

## Goal

This service must be configurable by operators with limited technical knowledge.

The primary configuration approach is:
- `appsettings.json` for normal deployments
- optional environment-specific overrides
- strongly validated settings at startup

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
- Must be a valid IPv4 multicast address if used.
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
- the service may rely on default OS routing/interface behavior

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

## Logging settings

Use standard .NET logging configuration.

### `Logging:LogLevel:Default`
Controls overall logging level.

Recommended production value:

    "Default": "Information"

Recommended troubleshooting value:

    "Default": "Debug"

### Suggested logging policy
- `Information`: startup/shutdown, config validation, major state changes
- `Warning`: transient errors, malformed packets, recoverable failures
- `Error`: hard failures and startup-blocking issues
- `Debug`: per-packet summaries and rewrite details

Normal production deployments should not emit per-packet logs.

---

## Update settings

Self-update is optional but supported.

### `Update:Enabled`
Enables update checks.

Example:

    "Enabled": true

### `Update:Channel`
Optional release channel name.

Example:

    "Channel": "stable"

### `Update:Repository`
Public GitHub repository identifier.

Example:

    "Repository": "your-org/your-repo"

### `Update:CheckIntervalHours`
How often the service checks for updates.

Example:

    "CheckIntervalHours": 24

Rules:
- Do not store credentials or tokens here for public release checks.
- If updates are disabled, the service must continue operating normally.

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
    "SendInterfaceIP": "198.51.100.20"
  },
  "Rewrite": {
    "PayloadRewriteSourceSubnet": "192.0.2.",
    "PayloadRewriteDestinationSubnet": "198.51.100."
  },
  "Update": {
    "Enabled": true,
    "Channel": "stable",
    "Repository": "your-org/your-repo",
    "CheckIntervalHours": 24
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
