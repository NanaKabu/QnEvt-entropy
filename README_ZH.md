# QnEvt Entropy

QnEvt Entropy 用于存放 QnEvt 物理熵节点相关的公开客户端和硬件集成文档。

这个仓库只公开适合审阅、集成和测试的部分，不公开私有固件内部实现、板卡设计文件、部署配置、凭证或生产数据。

English documentation: [README.md](README.md).

## 公开范围

这里公开：

- `client/` - Windows Avalonia 客户端，用于读取 QnEvt 熵节点数据包，并将遥测上传到配置的 endpoint。
- `docs/firmware_protocol.md` - 节点输出协议的公开字节级规范。
- `docs/firmware_protocol_ZH.md` - 固件协议中文文档。
- `hardware/README.md` - 硬件公开集成边界。
- `hardware/README_ZH.md` - 硬件公开集成边界中文文档。

以下内容不公开：

- 固件源码内部实现
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
hardware/
  README.md
  README_ZH.md
```

## 固件协议

公开固件契约以字节流协议形式记录：

- 同步字节 `0xAA`
- 包类型
- payload 长度
- payload
- CRC16-CCITT

协议文档描述随机输出、遥测、告警、错误和控制包。它不要求公开固件源码。

见 [docs/firmware_protocol_ZH.md](docs/firmware_protocol_ZH.md)。

## 客户端

Windows 客户端公开是为了让节点运营者可以检查主机如何读取带帧的熵包、处理遥测，并使用配置的 push 凭证上传批次。

见 [client/README.md](client/README.md)。

## 许可证

QnEvt Entropy 公开代码和文档使用 GNU Affero General Public License v3.0。见 [LICENSE](LICENSE)。
