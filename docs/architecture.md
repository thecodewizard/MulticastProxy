# Architecture

## Overview

This service relays multicast traffic between two Layer 3 networks that do not natively pass multicast across the routed boundary.

Each deployed instance can:
1. listen for multicast traffic on one or more configured UDP ports,
2. forward the received payload to a remote peer over unicast UDP,
3. receive unicast tunnel packets from the remote peer,
4. re-emit the payload as multicast on the destination network,
5. optionally rewrite subnet references inside the payload before re-emitting.

The service is intended to run unattended as a Windows Service and must remain stable during transient network issues, interface changes, and remote peer outages.

---

## Problem being solved

Some devices or applications use multicast or multicast-like discovery patterns on a local network. Routed Layer 3 boundaries usually prevent this traffic from traversing to another network segment.

This service provides a controlled relay mechanism:
- **local multicast in**
- **unicast tunnel across Layer 3**
- **remote multicast out**

This is not intended to become a general-purpose router or bridge. It is a narrowly scoped application-layer relay.

---

## High-level packet flow

### Direction A -> B
1. Service A joins the configured multicast group(s) on its local interface.
2. Service A receives a multicast UDP datagram on a configured multicast port.
3. Service A optionally wraps the datagram in a small relay envelope.
4. Service A sends the datagram over unicast UDP to Service B on the configured tunnel port.
5. Service B receives the unicast relay packet.
6. Service B optionally rewrites payload content.
7. Service B re-emits the resulting payload to the configured multicast group and original multicast port on its local network.

### Direction B -> A
The same flow occurs in reverse using the same service behavior on the opposite side.

---

## Deployment model

The intended deployment model is **one instance per network**.

Example:

- **Site A**
  - Local multicast source network
  - Relay service instance A
- **Site B**
  - Local multicast destination network
  - Relay service instance B

Each instance is configured with:
- the remote peer IP,
- the tunnel port,
- one or more multicast ports,
- the multicast group,
- optional interface bindings,
- optional payload rewrite settings.

This allows the same binary and same service design to operate on both ends.

---

## Core design principles

### 1. One codebase, configuration-driven behavior
The service must not require separate client/server binaries. Both behaviors should be handled by the same code and activated through configuration.

### 2. Preserve payload bytes unless explicitly rewriting
The relay should treat payloads as opaque bytes by default. Only rewrite payload content if rewrite settings are enabled and the payload format permits safe modification.

### 3. Fail safe
Configuration errors should prevent startup with a clear explanation. Runtime network failures should not crash the service.

### 4. Operational simplicity
Configuration must be understandable by non-developers. Logging must be useful without overwhelming the Windows Event Log.

### 5. Public-repo safety
No secrets, private endpoints, or customer-specific values may be embedded in source code.

### 6. Keep upgrades external to the service
The running service must not perform self-updates. New versions are distributed as MSI packages and applied manually or through external deployment tooling.

---

## Host layer

### Windows Service host
The application should use:
- .NET 8 Worker Service
- Generic Host
- `UseWindowsService()`

Responsibilities:
- lifecycle startup/shutdown,
- dependency injection,
- configuration loading,
- logging configuration,
- hosted background service registration.

---

## Configuration layer

### Typed options
Use typed options classes, for example:

- `RelayOptions`
- `RewriteOptions`

Responsibilities:
- bind configuration from `appsettings.json`,
- validate all required fields,
- reject invalid startup config,
- provide clean dependency injection to services.

Recommended validation:
- port ranges must be 0-65535,
- destination IP must parse,
- multicast group must parse if configured,
- rewrite source and destination subnets must both be present if rewrite is enabled,
- interface IPs must parse if provided.

---

## Packet processing layer

### Multicast receive service
Responsibilities:
- bind UDP listeners for configured multicast ports,
- join the configured multicast group,
- optionally bind to a specified local interface,
- receive multicast datagrams,
- hand packets to the tunnel sender pipeline.

Notes:
- socket reuse options may be required,
- listener behavior must be explicit and documented,
- receive loops must support cancellation cleanly.

### Tunnel sender service
Responsibilities:
- transmit relay packets as unicast UDP to the configured remote peer,
- include enough metadata for the receiver to know the original multicast port,
- handle transient send failures,
- log operational failures at the correct level.

### Tunnel receiver service
Responsibilities:
- listen on the configured unicast tunnel port,
- validate incoming relay packets,
- reject malformed relay envelopes,
- hand payloads to the multicast emitter pipeline.

### Multicast emit service
Responsibilities:
- re-emit received payloads onto the configured multicast group and original multicast port,
- optionally bind outgoing multicast to a chosen interface,
- keep multicast scope controlled,
- log send failures without crashing the service.

---

## Payload rewrite layer

### Rewrite service
Responsibilities:
- inspect payload bytes only if rewrite is enabled,
- attempt safe rewrite of source subnet references to destination subnet references,
- preserve original bytes on failure,
- avoid corrupting non-text or binary payloads.

Recommended behavior:
- if rewrite is disabled, return original bytes untouched,
- if payload cannot be safely interpreted for rewrite, pass through unchanged and log at debug/warning level,
- do not assume ASCII unless the protocol guarantees ASCII,
- avoid broad regex replacements that could rewrite unintended data.

Preferred implementation strategy:
1. Try to detect whether the payload is valid text in the expected encoding.
2. Only rewrite exact configured subnet strings.
3. Preserve unrelated data.
4. If uncertain, leave the payload unchanged.

---

## Relay envelope design

A small relay envelope is strongly recommended instead of sending raw payload bytes blindly over the tunnel.

Suggested envelope fields:
- protocol version
- original multicast port
- flags
- payload length
- payload bytes

Optional future fields:
- sender instance ID
- packet ID
- timestamp
- hop count or relay depth field

Benefits:
- the receiving side knows which multicast port to re-emit on,
- future protocol evolution becomes easier,
- loop-prevention metadata can be added cleanly,
- malformed tunnel payloads can be rejected early.

---

## Loop prevention

Loop prevention is important if:
- both peers relay the same multicast domains bidirectionally,
- a re-emitted packet can be observed again by the originating side,
- the network topology may reflect or duplicate traffic.

At minimum, one of these protections should exist:

### Option A: instance ID + seen cache
Each relay packet contains:
- sender instance ID
- packet fingerprint or packet ID

The receiving service keeps a short-lived cache of recently seen packet IDs and does not re-forward duplicates.

### Option B: hop count
Each relay packet contains a hop count.
- locally received multicast packets are sent with hop count = 1,
- the receiver decrements or rejects if already relayed.

### Option C: source marking
Track locally emitted packets and suppress immediate re-forwarding if the same packet reappears.

Preferred approach:
- **instance ID + short deduplication cache**

This is more robust than a simple hop count.

---

## Logging design

Logging should use standard .NET logging and the Windows Event Log provider.

### Normal mode
Should log:
- service startup and shutdown,
- configuration validation failures,
- multicast join/bind failures,
- tunnel listener startup,
- remote peer send failures,
- major warnings and errors.

Should not log:
- every packet,
- repetitive identical warnings without throttling.

### Debug mode
May additionally log:
- packet receive summaries,
- packet send summaries,
- rewrite decisions,
- socket details,
- deduplication decisions,
- remote endpoint details.

### Throttling / noise control
For recurring failures, use throttling or aggregation where practical to avoid flooding Event Log.

---

## Deployment and upgrades

This service does not perform self-updates.

New versions are distributed as MSI installers. Upgrade execution is handled manually or by external software deployment tooling. The installer is responsible for stopping the old service version, replacing binaries, and starting the updated version again.

This keeps the runtime service focused on packet relay and avoids unsafe in-process upgrade behavior.

---

## Recommended project layout

```text
/src
  /MulticastRelay.Service
    Program.cs
    appsettings.json
    /Options
      RelayOptions.cs
      RewriteOptions.cs
    /Validation
      RelayOptionsValidator.cs
    /Services
      RelayWorker.cs
      MulticastReceiver.cs
      TunnelSender.cs
      TunnelReceiver.cs
      MulticastEmitter.cs
      PayloadRewriteService.cs
      DeduplicationService.cs
    /Protocol
      RelayEnvelope.cs
      RelayEnvelopeSerializer.cs
    /Logging
      EventLogExtensions.cs

/tests
  /MulticastRelay.Service.Tests
    RelayOptionsValidationTests.cs
    PayloadRewriteTests.cs
    RelayEnvelopeTests.cs
    DeduplicationTests.cs

/docs
  architecture.md
  configuration.md
