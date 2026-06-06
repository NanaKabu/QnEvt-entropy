cmake_minimum_required(VERSION 3.13)

set(PICO_BOARD pico2 CACHE STRING "Board type")
set(QNEVT_I2C_SDA_PIN 4 CACHE STRING "I2C SDA GPIO pin")
set(QNEVT_I2C_SCL_PIN 5 CACHE STRING "I2C SCL GPIO pin")
set(QNEVT_I2C_BAUD_HZ 400000 CACHE STRING "I2C bus speed in Hz")
set(QNEVT_XSHUT_PIN -1 CACHE STRING "Optional VL53L1X XSHUT GPIO pin, or -1 to disable")

include(${CMAKE_CURRENT_LIST_DIR}/../../pico_sdk_import.cmake)

project(qnevt_pico2_vl53l1x_minimal C CXX ASM)
set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)

pico_sdk_init()

include(${CMAKE_CURRENT_LIST_DIR}/../../third_party/VL53L1X-C-API-Pico/library/import.cmake)

add_executable(qnevt_vl53l1x_minimal
  main.c
  vl53l1x_pico_platform.c
)

target_link_libraries(qnevt_vl53l1x_minimal
  pico_stdlib
  hardware_i2c
  VL53L1X_pico_api
)

target_compile_definitions(qnevt_vl53l1x_minimal PRIVATE
  QNEVT_I2C_SDA_PIN=${QNEVT_I2C_SDA_PIN}
  QNEVT_I2C_SCL_PIN=${QNEVT_I2C_SCL_PIN}
  QNEVT_I2C_BAUD_HZ=${QNEVT_I2C_BAUD_HZ}
  QNEVT_XSHUT_PIN=${QNEVT_XSHUT_PIN}
)

pico_enable_stdio_usb(qnevt_vl53l1x_minimal 1)
pico_enable_stdio_uart(qnevt_vl53l1x_minimal 0)
pico_add_extra_outputs(qnevt_vl53l1x_minimal)
