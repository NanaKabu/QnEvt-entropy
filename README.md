# QnEvt Entropy

QnEvt Entropy contains the public client, protocol documents, and Pico 2 firmware baseline for QnEvt physical entropy nodes.

This repository is for the parts that can be inspected, integrated, and tested publicly without exposing private entropy algorithms, board design files, deployment configuration, credentials, or production data.

中文文档见 [README_ZH.md](README_ZH.md).

## Public Scope

Published here:

- `client/` - Windows Avalonia client for reading QnEvt entropy node packets and uploading telemetry to a configured endpoint.
- `firmware/pico2-vl53l1x-minimal/` - minimal Raspberry Pi Pico 2 + VL53L1X Pico SDK firmware.
- `docs/firmware_protocol.md` - public byte-level node output protocol.
- `docs/firmware_protocol_ZH.md` - Chinese version of the firmware protocol.
- `docs/wiring.md` - Pico 2 and VL53L1X wiring notes.
- `docs/privacy.md` - public release and privacy checklist.
- `hardware/README.md` - public hardware integration boundary.
- `hardware/README_ZH.md` - Chinese version of the hardware integration boundary.

Intentionally not published:

- production firmware internals
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
  wiring.md
  wiring_ZH.md
firmware/
  pico2-vl53l1x-minimal/
hardware/
  README.md
  README_ZH.md
third_party/
  VL53L1X-C-API-Pico/
```

## Pico 2 Firmware

The public Pico 2 baseline is a small Pico SDK C example for a VL53L1X time-of-flight sensor. It uses hardware I2C0 by default:

```text
Pico 2 GP4  ->  VL53L1X SDA
Pico 2 GP5  ->  VL53L1X SCL
Pico 2 3V3  ->  VL53L1X VIN/VCC
Pico 2 GND  ->  VL53L1X GND
```

Build:

```powershell
cd firmware/pico2-vl53l1x-minimal
mkdir build
cd build
cmake -DPICO_BOARD=pico2 ..
cmake --build .
```

See [docs/wiring.md](docs/wiring.md) and [firmware/README.md](firmware/README.md).

## Firmware Protocol

The public firmware contract is documented as a byte stream protocol:

- sync byte `0xAA`
- packet type
- payload length
- payload
- CRC16-CCITT

The protocol documents random output, telemetry, warning, error, and control packets. The minimal Pico 2 firmware baseline is published here; production firmware internals and private entropy-conditioning logic remain outside this public scope.

See [docs/firmware_protocol.md](docs/firmware_protocol.md).

## Client

The Windows client is public so node operators can inspect how the host reads framed entropy packets, handles telemetry, and uploads batches with a configured push credential.

See [client/README.md](client/README.md).

## License

QnEvt Entropy public code and documentation are released under the GNU Affero General Public License v3.0. The bundled VL53L1X C API keeps its upstream license. See [LICENSE](LICENSE) and [third_party/VL53L1X-C-API-Pico/LICENSE](third_party/VL53L1X-C-API-Pico/LICENSE).
