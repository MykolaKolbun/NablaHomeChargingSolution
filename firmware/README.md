# Firmware

| Folder | Board | Purpose |
|---|---|---|
| `esp32/ev-charger-esp/` | ESP32-C6-Zero | Wi-Fi, OCPP 1.6J client (MicroOcpp) → `wss://home-ocpp.alternatiview.com.ua/ws/30011` |
| `stm32/` | Blue Pill STM32F103C8T6 | Control Pilot, safety (STM32CubeIDE project, not yet created) |

Hardware notes, wiring, Arduino IDE board settings: [../docs/diy-charger-controller.md](../docs/diy-charger-controller.md).
DIY hardware development is paused (2026-10-09); station 30011 stays connected as-is.

## ESP32 sketch

1. Copy `secrets.h.example` → `secrets.h`, fill Wi-Fi credentials (`secrets.h` is git-ignored).
2. Arduino IDE → **File → Preferences → Sketchbook location** = this repo's `firmware/esp32`
   (then libraries come from `firmware/esp32/libraries`), or keep your own sketchbook with the
   same library versions.
3. Open `ev-charger-esp/ev-charger-esp.ino` (folder name = sketch name), board ESP32C6 Dev Module.

## Libraries (not in git — `firmware/esp32/libraries/` is ignored)

| Library | Version | Source |
|---|---|---|
| MicroOcpp | **1.2.0** | ZIP from github.com/matth-x/MicroOcpp (not in Library Manager) |
| ArduinoJson | **6.21.5** — not 7.x (MicroOcpp 1.2.0 needs 6) | Library Manager |
| WebSockets (Markus Sattler) | 2.7.2 | Library Manager |

> The copy currently in `firmware/esp32/libraries/` has ArduinoJson **7.4.3**. If you switch the
> sketchbook to this folder, downgrade it to 6.21.5 in Library Manager first.
> `UIPEthernet` there is not needed for the Wi-Fi build.
