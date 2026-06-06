# QnEvt Entropy

QnEvt Entropy 用于存放 QnEvt 物理熵节点相关的公开客户端、协议文档和 Pico 2 固件基线。

这个仓库只公开适合审阅、集成和测试的部分，不公开私有熵算法、板卡设计文件、部署配置、凭证或生产数据。

English documentation: [README.md](README.md).

## 公开范围

这里公开：

- `client/` - Windows Avalonia 客户端，用于读取 QnEvt 熵节点数据包，并将遥测上传到配置的 endpoint。
- `firmware/pico2-vl53l1x-minimal/` - Raspberry Pi Pico 2 + VL53L1X 的最小 Pico SDK 固件。
- `docs/firmware_protocol.md` - 节点输出协议的公开字节级规范。
- `docs/firmware_protocol_ZH.md` - 固件协议中文文档。
- `docs/wiring.md` - Pico 2 和 VL53L1X 接线说明。
- `docs/privacy.md` - 公开发布和隐私检查清单。
- `hardware/README.md` - 硬件公开集成边界。
- `hardware/README_ZH.md` - 硬件公开集成边界中文文档。

以下内容不公开：

- 生产固件内部实现
- 私有熵调理逻辑
- 私有校准值和阈值
- PCB、原理图、CAD、供应商或制造文件
- 服务端实现和管理后台
- 部署配置、凭证、日志、数据库、生成二进制或采集数据

## 目录结构

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

## Pico 2 固件

公开的 Pico 2 基线是一个小型 Pico SDK C 示例，用于 VL53L1X ToF 测距传感器。默认使用硬件 I2C0：

```text
Pico 2 GP4  ->  VL53L1X SDA
Pico 2 GP5  ->  VL53L1X SCL
Pico 2 3V3  ->  VL53L1X VIN/VCC
Pico 2 GND  ->  VL53L1X GND
```

构建：

```powershell
cd firmware/pico2-vl53l1x-minimal
mkdir build
cd build
cmake -DPICO_BOARD=pico2 ..
cmake --build .
```

见 [docs/wiring_ZH.md](docs/wiring_ZH.md) 和 [firmware/README_ZH.md](firmware/README_ZH.md)。

## 固件协议

公开固件契约以字节流协议形式记录：

- 同步字节 `0xAA`
- 包类型
- payload 长度
- payload
- CRC16-CCITT

协议文档描述随机输出、遥测、告警、错误和控制包。这里已公开最小 Pico 2 固件基线；生产固件内部实现和私有熵调理逻辑不在这个公开范围内。

见 [docs/firmware_protocol_ZH.md](docs/firmware_protocol_ZH.md)。

## 客户端

Windows 客户端公开是为了让节点运营者可以检查主机如何读取带帧的熵包、处理遥测，并使用配置的 push 凭证上传批次。

见 [client/README.md](client/README.md)。

## 许可证

QnEvt Entropy 公开代码和文档使用 GNU Affero General Public License v3.0。内置的 VL53L1X C API 保留其上游许可证。见 [LICENSE](LICENSE) 和 [third_party/VL53L1X-C-API-Pico/LICENSE](third_party/VL53L1X-C-API-Pico/LICENSE)。
