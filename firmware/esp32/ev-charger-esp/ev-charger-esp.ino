#include <WiFi.h>
#include <time.h>
#include <MicroOcpp.h>
#include "secrets.h"

#define OCPP_BACKEND   "wss://home-ocpp.alternatiview.com.ua/ws"   // EVOCPP (Nabla Home); ID додасться сам
#define CHARGE_BOX_ID  "30011"

static void connectWiFi() {
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  Serial.print("Wi-Fi");
  while (WiFi.status() != WL_CONNECTED) { delay(500); Serial.print("."); }
  Serial.printf("\nIP: %s, RSSI: %d dBm\n",
                WiFi.localIP().toString().c_str(), WiFi.RSSI());
}

static void syncTime() {
  configTzTime("EET-2EEST,M3.5.0/3,M10.5.0/4", "pool.ntp.org", "time.google.com");
  struct tm tm;
  while (!getLocalTime(&tm, 5000)) Serial.println("SNTP retry...");
  Serial.printf("Time: %04d-%02d-%02d %02d:%02d:%02d\n", tm.tm_year + 1900,
                tm.tm_mon + 1, tm.tm_mday, tm.tm_hour, tm.tm_min, tm.tm_sec);
}

void setup() {
  Serial.begin(115200);
  delay(2000);
  connectWiFi();
  syncTime();

  mocpp_initialize(OCPP_BACKEND, CHARGE_BOX_ID,
                   "DIY-Proxy",      // chargePointModel
                   "Home");          // chargePointVendor

  // Заглушки, поки немає CP і лічильника
  setConnectorPluggedInput([]() { return false; });  // авто не підключене
  setEnergyMeterInput([]()      { return 0.f; });    // Wh
  setPowerMeterInput([]()       { return 0.f; });    // W

  Serial.println("OCPP started");
}

void loop() {
  mocpp_loop();

  static uint32_t last = 0;
  if (millis() - last > 1000) {           // блимання синім = прошивка жива
    last = millis();
    static bool on = false;
    on = !on;
    rgbLedWrite(8, 0, 0, on ? 30 : 0);
  }
}