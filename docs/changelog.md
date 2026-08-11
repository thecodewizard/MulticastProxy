# Changelog

## 2026-08-11

### Added

- Added payload rewrite support for discovery packets where the device advertises an on-site subnet but remote users must connect through a NAT-visible address.
- Added checksum refresh logic for discovery payloads that include an inline XOR-style CRC field, so rewritten packets remain valid to the receiving RX client.
- Added regression coverage for the discovery rewrite case to confirm both the IP text replacement and checksum update stay correct.

### Changed

- Clarified in code comments that rewrite is intentionally applied on the multicast emit side, so only the remote segment sees the translated discovery address.
- Clarified in code comments and installer configuration that the relay is installed per-machine and runs as a Windows service for all users, which is appropriate for RDS hosts.

### Fixed

- Fixed the discovery relay behavior for NAT environments where the scanner is reached directly on-site but must be reached through a translated address from the cloud side.
- Fixed a packet validity issue where rewriting the advertised scanner IP without updating the discovery CRC caused the scanner to appear present but remain unusable from the remote RX application.

### Verified

- Verified that RX discovery and scan initiation succeed through the cloud relay path after the NAT-aware rewrite and checksum fix.
- Verified that the remaining RX startup delay was not caused by the multicast relay. Packet capture analysis showed the application performing its own HTTP and FTP startup activity, including job synchronization, while the relay path itself remained healthy.

### Operational note

- This change set intentionally supports the multicast discovery step only. After discovery, follow-up traffic such as FTP, HTTP, or ICMP is expected to go directly to the translated device address through the network and NAT path rather than through the multicast relay itself.
