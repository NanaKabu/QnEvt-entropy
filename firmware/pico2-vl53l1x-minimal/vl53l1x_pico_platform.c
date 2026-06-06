#include <string.h>

#include "hardware/i2c.h"
#include "pico/stdlib.h"

#include "VL53L1X_api.h"
#include "VL53L1X_platform.h"
#include "VL53L1X_types.h"

static i2c_inst_t *active_i2c = NULL;
static uint8_t buffer[VL53L1X_I2C_BUF_SIZE + 2];

VL53L1X_Status_t VL53L1X_I2C_Init(uint16_t dev, i2c_inst_t *i2c_device) {
  active_i2c = i2c_device;

  uint16_t sensor_id = 0;
  VL53L1X_Status_t status = VL53L1X_GetSensorId(dev, &sensor_id);
  if (status != 0 || sensor_id != VL53L1X_SENSOR_ID) {
    return -1;
  }

  return 0;
}

VL53L1X_Status_t VL53L1X_WaitMs(uint16_t dev, int32_t wait_ms) {
  (void)dev;
  sleep_ms(wait_ms);
  return 0;
}

VL53L1X_Status_t VL53L1X_WriteMulti(uint16_t dev, uint16_t index, uint8_t *data, uint32_t count) {
  if (active_i2c == NULL || (count + 2) > sizeof(buffer)) {
    return -1;
  }

  buffer[0] = (uint8_t)(index >> 8);
  buffer[1] = (uint8_t)(index & 0xFF);
  memcpy(&buffer[2], data, count);

  int written = i2c_write_blocking(active_i2c, (uint8_t)dev, buffer, count + 2, false);
  return written == (int)(count + 2) ? 0 : -1;
}

VL53L1X_Status_t VL53L1X_ReadMulti(uint16_t dev, uint16_t index, uint8_t *data, uint32_t count) {
  if (active_i2c == NULL || count > VL53L1X_I2C_BUF_SIZE) {
    return -1;
  }

  uint8_t reg[2] = {
      (uint8_t)(index >> 8),
      (uint8_t)(index & 0xFF),
  };

  int written = i2c_write_blocking(active_i2c, (uint8_t)dev, reg, sizeof(reg), true);
  if (written != (int)sizeof(reg)) {
    return -1;
  }

  int read = i2c_read_blocking(active_i2c, (uint8_t)dev, data, count, false);
  return read == (int)count ? 0 : -1;
}

VL53L1X_Status_t VL53L1X_RdByte(uint16_t dev, uint16_t index, uint8_t *data) {
  return VL53L1X_ReadMulti(dev, index, data, 1);
}

VL53L1X_Status_t VL53L1X_RdWord(uint16_t dev, uint16_t index, uint16_t *data) {
  VL53L1X_Status_t status = VL53L1X_ReadMulti(dev, index, (uint8_t *)data, 2);
  *data = ntohs(*data);
  return status;
}

VL53L1X_Status_t VL53L1X_RdDWord(uint16_t dev, uint16_t index, uint32_t *data) {
  VL53L1X_Status_t status = VL53L1X_ReadMulti(dev, index, (uint8_t *)data, 4);
  *data = ntohl(*data);
  return status;
}

VL53L1X_Status_t VL53L1X_WrByte(uint16_t dev, uint16_t index, uint8_t data) {
  return VL53L1X_WriteMulti(dev, index, &data, 1);
}

VL53L1X_Status_t VL53L1X_WrWord(uint16_t dev, uint16_t index, uint16_t data) {
  data = htons(data);
  return VL53L1X_WriteMulti(dev, index, (uint8_t *)&data, 2);
}

VL53L1X_Status_t VL53L1X_WrDWord(uint16_t dev, uint16_t index, uint32_t data) {
  data = htonl(data);
  return VL53L1X_WriteMulti(dev, index, (uint8_t *)&data, 4);
}
