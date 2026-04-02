# MulticastProxy

MulticastProxy is a Windows Service that relays multicast traffic between two Layer 3 networks.

It is designed for environments where multicast discovery or application traffic must cross a routed boundary, but native multicast routing is unavailable, undesirable, or too complex for the use case.

The service works by:
1. listening for multicast traffic on one or more configured UDP ports,
2. forwarding the received packet to a remote peer over unicast UDP,
3. receiving the packet on the remote peer,
4. re-emitting it as multicast on the destination network,
5. optionally rewriting subnet references inside the payload when needed.

---

## Project status

This repository currently focuses on a production-oriented design and implementation path for the service.

The intended target is:
- .NET 8
- Windows Service hosting
- configuration through `appsettings.json`
- Windows Event Log logging
- MSI-based installation and upgrades
- safe, maintainable production code rather than a one-off proof of concept

This service does **not** self-update. New versions are distributed as MSI packages and upgraded manually or through external deployment tooling.

---

## Use case

Some devices and applications rely on multicast traffic for discovery or communication on a local subnet. That traffic usually does not cross a Layer 3 boundary by default.

MulticastProxy provides a controlled relay model:

- multicast traffic is received locally
- the payload is tunneled to a remote instance using unicast UDP
- the remote instance re-emits it as multicast on the destination network

This makes it possible to bridge multicast-dependent traffic between two routed networks without implementing full multicast routing.

---

## Key features

- Windows Service design
- support for one or more multicast UDP ports
- unicast tunnel between two peers
- optional payload subnet rewrite
- typed configuration with startup validation
- Windows Event Log integration
- support for explicit interface binding on multi-NIC systems
- production-friendly architecture with clear separation of concerns

---

## Non-goals

This project is **not** intended to be:
- a full Layer 2 bridge
- a general multicast router
- an automatic self-updating agent
- a catch-all network proxy for arbitrary protocols

The focus is narrow and intentional: relay selected multicast traffic safely and predictably.

---

## High-level architecture

Each instance performs the same role and is configured to point at its peer.

Example flow:

- Site A receives multicast traffic on a configured group and port
- Site A forwards the packet to Site B over unicast UDP
- Site B receives the packet on the tunnel port
- Site B optionally rewrites subnet references in the payload
- Site B re-emits the packet as multicast on its local network

This same pattern works in both directions.

For more detail, see:
- `docs/architecture.md`
- `docs/configuration.md`

---

## Repository structure

```text
.
├── AGENTS.md
├── README.md
├── docs
│   ├── architecture.md
│   ├── configuration.md
│   └── appsettings.example.json
