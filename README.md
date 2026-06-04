# QnEvt Entropy

QnEvt Entropy contains the public client and hardware-facing integration documents for QnEvt physical entropy nodes.

This repository is for the parts that can be inspected, integrated, and tested publicly without exposing private firmware internals, board design files, deployment configuration, credentials, or production data.

中文文档见 [README_ZH.md](README_ZH.md).

## Public Scope

Published here:

- `client/` - Windows Avalonia client for reading QnEvt entropy node packets and uploading telemetry to a configured endpoint.
- `docs/firmware_protocol.md` - public byte-level node output protocol.
- `docs/firmware_protocol_ZH.md` - Chinese version of the firmware protocol.
- `hardware/README.md` - public hardware integration boundary.
- `hardware/README_ZH.md` - Chinese version of the hardware integration boundary.

Intentionally not published:

- firmware source internals
- private entropy conditioning logic
- private calibration values and thresholds
- PCB, schematic, CAD, supplier, or manufacturing files
- server implementation and admin backend
- deployment configuration, credentials, logs, databases, generated binaries, or captures

## Repository Layout

```text
client/
  Qnevt.csproj
  ViewModels/
  Views/
  Assets/
docs/
  firmware_protocol.md
  firmware_protocol_ZH.md
hardware/
  README.md
  README_ZH.md
```

## Firmware Protocol

The public firmware contract is documented as a byte stream protocol:

- sync byte `0xAA`
- packet type
- payload length
- payload
- CRC16-CCITT

The protocol documents random output, telemetry, warning, error, and control packets. It does not require publishing firmware source.

See [docs/firmware_protocol.md](docs/firmware_protocol.md).

## Client

The Windows client is public so node operators can inspect how the host reads framed entropy packets, handles telemetry, and uploads batches with a configured push credential.

See [client/README.md](client/README.md).

## License

QnEvt Entropy public code and documentation are released under the GNU Affero General Public License v3.0. See [LICENSE](LICENSE).
