# Firmware Examples

This directory contains the minimal public Pico SDK firmware:

- `pico2-vl53l1x-minimal/` initializes VL53L1X continuous ranging and prints distance data over USB serial.

It is intentionally plain C with Pico SDK APIs only. Product-specific entropy logic, calibration data, thresholds, credentials, logs, and board files are not included.
