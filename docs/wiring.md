# Wiring

Default examples use I2C0 on the Raspberry Pi Pico 2:

| Pico 2 | VL53L1X breakout | Notes |
| --- | --- | --- |
| GP4 | SDA | I2C data |
| GP5 | SCL | I2C clock |
| 3V3 | VIN or VCC | Use 3.3 V unless your breakout explicitly supports another voltage |
| GND | GND | Common ground |

Optional pins:

| Pico 2 | VL53L1X breakout | Notes |
| --- | --- | --- |
| GP6 | XSHUT | Optional sensor reset/shutdown control |
| GP7 | GPIO1 or INT | Optional interrupt output |

## Notes

- VL53L1X uses I2C address `0x29` by default.
- Many breakout boards include I2C pull-up resistors. If yours does not, add appropriate pull-ups to 3.3 V.
- Keep wires short while testing. Long jumper wires can make 400 kHz I2C unreliable.
- The Pico SDK example uses hardware I2C0 on GP4 and GP5 by default. Override `QNEVT_I2C_SDA_PIN` and `QNEVT_I2C_SCL_PIN` with CMake options only when using another valid hardware-I2C pin pair.

## Text Wiring Diagram

```text
Raspberry Pi Pico 2              VL53L1X breakout
-------------------              ----------------
GP4  / I2C0 SDA        ------->   SDA
GP5  / I2C0 SCL        ------->   SCL
3V3                  ---------->  VIN or VCC
GND                  ---------->  GND

Optional:
GP6                  ---------->  XSHUT
GP7                  <----------  GPIO1 or INT
```
