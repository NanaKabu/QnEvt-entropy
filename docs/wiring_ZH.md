# 接线说明

默认示例使用 Raspberry Pi Pico 2 的 I2C0：

| Pico 2 | VL53L1X 模块 | 说明 |
| --- | --- | --- |
| GP4 | SDA | I2C 数据线 |
| GP5 | SCL | I2C 时钟线 |
| 3V3 | VIN 或 VCC | 除非模块明确支持其他电压，否则建议使用 3.3 V |
| GND | GND | 共地 |

可选引脚：

| Pico 2 | VL53L1X 模块 | 说明 |
| --- | --- | --- |
| GP6 | XSHUT | 可选的传感器复位/关断控制 |
| GP7 | GPIO1 或 INT | 可选中断输出 |

## 注意事项

- VL53L1X 默认 I2C 地址是 `0x29`。
- 很多模块已经带有 I2C 上拉电阻。如果你的模块没有上拉，请添加到 3.3 V 的合适上拉电阻。
- 测试阶段尽量使用较短的线。过长的杜邦线可能让 400 kHz I2C 不稳定。
- Pico SDK 示例默认使用 GP4 和 GP5 上的硬件 I2C0。只有在改用另一组有效硬件 I2C 引脚时，才通过 CMake 选项覆盖 `QNEVT_I2C_SDA_PIN` 和 `QNEVT_I2C_SCL_PIN`。

## 文本接线图

```text
Raspberry Pi Pico 2              VL53L1X 模块
-------------------              ------------
GP4  / I2C0 SDA        ------->   SDA
GP5  / I2C0 SCL        ------->   SCL
3V3                  ---------->  VIN 或 VCC
GND                  ---------->  GND

可选：
GP6                  ---------->  XSHUT
GP7                  <----------  GPIO1 或 INT
```
