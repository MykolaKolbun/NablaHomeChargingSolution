# Firmware

| Folder | Board | Purpose |
|---|---|---|
| `esp32/DiyProxyOcpp/` | ESP32-C6-Zero | Wi-Fi, OCPP 1.6J client (MicroOcpp) → `wss://home-ocpp.alternatiview.com.ua/ws/30011` |
| `stm32/` | Blue Pill STM32F103C8T6 | Control Pilot, safety (STM32CubeIDE project, not yet created) |

Hardware notes, wiring, Arduino IDE settings: [../docs/diy-charger-controller.md](../docs/diy-charger-controller.md).

## ESP32 sketch

1. Copy `secrets.h.example` → `secrets.h`, fill Wi-Fi credentials (file is git-ignored).
2. Open `DiyProxyOcpp/DiyProxyOcpp.ino` in Arduino IDE (folder name = sketch name).
3. Board/library settings — see the doc above (ESP32C6 Dev Module, MicroOcpp 1.2.0, ArduinoJson 6.21.5).
