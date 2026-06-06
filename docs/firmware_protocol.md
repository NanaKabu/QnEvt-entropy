# QnEvt Firmware Serial Protocol

This document describes the public byte-level contract between a QnEvt-compatible hardware entropy node and a host client.

It intentionally documents only the transport format and observable payloads. Firmware internals, board design files, private calibration values, health-test thresholds, and entropy conditioning implementation details are outside the public scope.

## Transport

Default binary transport:

| Field | Value |
| --- | --- |
| Physical link | UART or USB CDC byte stream |
| UART baud rate | `2000000` |
| UART TX | `GP0` |
| UART RX | `GP1` |
| Byte order | Big-endian for multi-byte payload values unless noted |

The host should treat the stream as a sequence of framed packets.

## Frame Format

```text
+--------+------+--------+---------+----------+
| Sync   | Type | Length | Payload | CRC16    |
| 1 byte | 1 B  | 1 B    | 0-255 B | 2 bytes  |
+--------+------+--------+---------+----------+
```

| Field | Description |
| --- | --- |
| `Sync` | Always `0xAA`. Hosts should resynchronize by scanning for this byte. |
| `Type` | Packet type. See packet table below. |
| `Length` | Payload length in bytes. |
| `Payload` | Packet-specific bytes. |
| `CRC16` | CRC16-CCITT over `Type || Length || Payload`, initial value `0xFFFF`, polynomial `0x1021`, stored little-endian as low byte then high byte. |

Minimum frame length is 5 bytes. Maximum frame length is 260 bytes.

## Packet Types

| Type | Name | Direction | Payload length | Description |
| --- | --- | --- | ---: | --- |
| `0x01` | `RANDOM` | node to host | `48` | Main entropy output packet. |
| `0x02` | `WARN` | node to host | `1` | Non-fatal health warning. |
| `0x03` | `TELEMETRY` | node to host | `10` | Compact health telemetry snapshot. |
| `0xE0` | `ERROR` | node to host | `1` | Fatal or output-stopping hardware/health error. |
| `0x88` | `CONTROL` | host to node | `0` or `1` | Optional control command. |

## `RANDOM` Payload

`RANDOM` packets contain 48 bytes:

| Offset | Length | Description |
| ---: | ---: | --- |
| `0` | `16` | Eight raw 16-bit sensor samples, big-endian. These are exposed for monitoring and bridge-side diagnostics. |
| `16` | `32` | Conditioned entropy block. Host clients should use this field as the random byte material. |

The host should ignore `RANDOM` packets when the node is in an error state and should stop accepting output after an `ERROR` packet until the device is reset or recovered by implementation-specific policy.

## `TELEMETRY` Payload

`TELEMETRY` packets contain 10 bytes:

| Offset | Length | Description |
| ---: | ---: | --- |
| `0` | `2` | Estimated entropy metric multiplied by 1000, big-endian unsigned integer. |
| `2` | `1` | Bit-0 high percentage. |
| `3` | `1` | Bit-1 high percentage. |
| `4` | `1` | Bit-2 high percentage. |
| `5` | `1` | Bit-3 high percentage. |
| `6` | `1` | Bit-4 high percentage. |
| `7` | `1` | Repetitive-count failure counter, low byte. |
| `8` | `1` | Adaptive-proportion failure counter, low byte. |
| `9` | `1` | Node health status: `0` OK, `1` warning, `2` fail. |

Host clients may display this information, include it in local diagnostics, or commit it as metadata next to uploaded entropy batches.

## Warning Codes

| Code | Meaning |
| --- | --- |
| `0x01` | Low entropy estimate warning. |
| `0x02` | Bit-bias warning. |

Warnings are non-fatal. The host may continue reading `RANDOM` packets while marking the batch with a degraded health status.

## Error Codes

| Code | Meaning |
| --- | --- |
| `0x01` | Health test failure. |
| `0x05` | VL53L1X I2C communication failure. |
| `0x06` | VL53L1X read timeout. |
| `0x07` | Sensor saturation or invalid range condition. |
| `0x08` | Sensor output appears stuck. |

Errors indicate that entropy output should be treated as unavailable. Host clients should stop accepting random bytes from the node until recovery is confirmed.

## Control Commands

`CONTROL` packets are sent from host to node:

| Payload | Meaning |
| --- | --- |
| empty payload or `00` | Reboot firmware. |
| `01` | Enter USB BOOTSEL mode. |

The same frame structure and CRC rule apply to host-to-node control packets.

## Host Parser Requirements

A compatible host parser should:

1. Scan for `0xAA`.
2. Read `Type`, `Length`, `Payload`, and the two CRC bytes.
3. Recompute CRC16-CCITT over `Type || Length || Payload`.
4. Drop frames with invalid CRC or unsupported length.
5. Treat `ERROR` as output-stopping.
6. Use only the 32-byte conditioned portion of `RANDOM` as random byte material.
7. Store or upload telemetry as metadata, not as secret material.

## Privacy Boundary

This protocol does not require publishing production firmware internals, calibration files, board design files, device serial numbers, credentials, production server URLs, or deployment configuration. The minimal Pico 2 firmware baseline is provided only as a public integration and testing reference. Public host integrations should rely on this contract rather than private firmware internals.
