# Public Hardware Integration Boundary

This directory documents only the public hardware-facing contract for QnEvt entropy nodes.

## Published

- Host-facing serial protocol.
- Default public link assumptions:
  - UART baud rate: `2000000`
  - UART TX: `GP0`
  - UART RX: `GP1`
  - USB CDC byte stream may expose the same framed packets.
- High-level node behavior:
  - emits conditioned entropy blocks
  - emits telemetry snapshots
  - emits warnings and output-stopping errors
  - accepts reboot and BOOTSEL control commands

## Not Published

- production firmware internals beyond the public minimal baseline
- sensor sampling implementation
- entropy conditioning implementation
- private health-test thresholds
- private calibration values
- board schematics, PCB layout, CAD files, BOM, supplier notes, or manufacturing files
- device serial numbers, node tokens, push keys, server URLs, or deployment configuration

## Integration

Host software should integrate through [../docs/firmware_protocol.md](../docs/firmware_protocol.md) rather than relying on private firmware or board internals.
