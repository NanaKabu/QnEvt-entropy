#include <stdio.h>

#include "hardware/gpio.h"
#include "hardware/i2c.h"
#include "pico/stdlib.h"

#include "VL53L1X_api.h"

#ifndef QNEVT_I2C_SDA_PIN
#define QNEVT_I2C_SDA_PIN 4
#endif

#ifndef QNEVT_I2C_SCL_PIN
#define QNEVT_I2C_SCL_PIN 5
#endif

#ifndef QNEVT_I2C_BAUD_HZ
#define QNEVT_I2C_BAUD_HZ (400 * 1000)
#endif

#ifndef QNEVT_XSHUT_PIN
#define QNEVT_XSHUT_PIN -1
#endif

#define QNEVT_I2C_PORT i2c0
#define VL53L1X_ADDR 0x29

static void init_optional_xshut(void) {
#if QNEVT_XSHUT_PIN >= 0
  gpio_init(QNEVT_XSHUT_PIN);
  gpio_set_dir(QNEVT_XSHUT_PIN, GPIO_OUT);
  gpio_put(QNEVT_XSHUT_PIN, 0);
  sleep_ms(10);
  gpio_put(QNEVT_XSHUT_PIN, 1);
  sleep_ms(10);
#endif
}

static void init_i2c_bus(void) {
  i2c_init(QNEVT_I2C_PORT, QNEVT_I2C_BAUD_HZ);
  gpio_set_function(QNEVT_I2C_SDA_PIN, GPIO_FUNC_I2C);
  gpio_set_function(QNEVT_I2C_SCL_PIN, GPIO_FUNC_I2C);
  gpio_pull_up(QNEVT_I2C_SDA_PIN);
  gpio_pull_up(QNEVT_I2C_SCL_PIN);
}

static bool wait_for_sensor_boot(void) {
  for (uint32_t i = 0; i < 1000; ++i) {
    uint8_t booted = 0;
    if (VL53L1X_BootState(VL53L1X_ADDR, &booted) == 0 && booted) {
      return true;
    }
    sleep_ms(2);
  }

  return false;
}

static void halt_with_message(const char *message) {
  while (true) {
    printf("error=%s\n", message);
    sleep_ms(1000);
  }
}

int main(void) {
  stdio_init_all();
  sleep_ms(2000);

  printf("Pico 2 + VL53L1X minimal firmware\n");
  printf("i2c_sda=GP%d,i2c_scl=GP%d,i2c_hz=%d,i2c_mode=hardware\n",
         QNEVT_I2C_SDA_PIN,
         QNEVT_I2C_SCL_PIN,
         QNEVT_I2C_BAUD_HZ);

  init_optional_xshut();
  init_i2c_bus();

  if (VL53L1X_I2C_Init(VL53L1X_ADDR, QNEVT_I2C_PORT) != 0) {
    halt_with_message("vl53l1x_i2c_init_failed");
  }

  uint16_t sensor_id = 0;
  if (VL53L1X_GetSensorId(VL53L1X_ADDR, &sensor_id) != 0) {
    halt_with_message("vl53l1x_sensor_id_read_failed");
  }
  printf("sensor_id=0x%04X\n", sensor_id);

  if (!wait_for_sensor_boot()) {
    halt_with_message("vl53l1x_boot_timeout");
  }

  if (VL53L1X_SensorInit(VL53L1X_ADDR) != 0) {
    halt_with_message("vl53l1x_sensor_init_failed");
  }

  if (VL53L1X_SetDistanceMode(VL53L1X_ADDR, 2) != 0) {
    halt_with_message("vl53l1x_set_distance_mode_failed");
  }

  if (VL53L1X_SetTimingBudgetInMs(VL53L1X_ADDR, 50) != 0) {
    halt_with_message("vl53l1x_set_timing_budget_failed");
  }

  if (VL53L1X_SetInterMeasurementInMs(VL53L1X_ADDR, 100) != 0) {
    halt_with_message("vl53l1x_set_intermeasurement_failed");
  }

  if (VL53L1X_StartRanging(VL53L1X_ADDR) != 0) {
    halt_with_message("vl53l1x_start_ranging_failed");
  }

  printf("time_ms,distance_mm,status,ambient,signal_per_spad,spads\n");

  while (true) {
    uint8_t ready = 0;
    for (uint32_t waited_ms = 0; waited_ms < 250 && !ready; waited_ms += 5) {
      if (VL53L1X_CheckForDataReady(VL53L1X_ADDR, &ready) != 0) {
        halt_with_message("vl53l1x_data_ready_failed");
      }
      sleep_ms(5);
    }

    if (!ready) {
      printf("warning=range_timeout\n");
      VL53L1X_ClearInterrupt(VL53L1X_ADDR);
      continue;
    }

    VL53L1X_Result_t result = {0};
    if (VL53L1X_GetResult(VL53L1X_ADDR, &result) != 0) {
      halt_with_message("vl53l1x_get_result_failed");
    }

    VL53L1X_ClearInterrupt(VL53L1X_ADDR);

    printf("%lu,%u,%u,%u,%u,%u\n",
           to_ms_since_boot(get_absolute_time()),
           result.distance,
           result.status,
           result.ambient,
           result.sigPerSPAD,
           result.numSPADs);

    sleep_ms(20);
  }
}
