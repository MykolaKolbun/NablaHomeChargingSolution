# OCPP Server

OCPP 1.6 WebSocket server for EV charger communication.

## Charger Connection

Configure your charger's OCPP backend URL to:

```
wss://ocpp.alternatiview.com.ua/ws/{chargePointId}
```

**Example for charger u030:**
```
wss://ocpp.alternatiview.com.ua/ws/u030
```

TLS is terminated by Cloudflare Tunnel — the server itself runs plain WebSocket internally.

The `chargePointId` must match the `OcppId` field set on the station in the EVChargingApi database.
> ⚠️ The chargePointId is **case-sensitive**. Check the logs to see exactly how the charger identifies itself.

## Viewing Logs on the Pi

Follow live logs (Ctrl+C to exit):
```bash
docker logs evchargingapi-ocpp-1 --tail 50 -f
```

Last 100 lines without following:
```bash
docker logs evchargingapi-ocpp-1 --tail 100
```

When a charger connects successfully you should see:
```
Station U030 connected.
Received CALL: BootNotification from U030
```

## Admin API

List all registered chargers (includes online status):
```
GET https://ocpp.alternatiview.com.ua/api/admin/chargers
```

Update charger info after it auto-registers:
```
PUT https://ocpp.alternatiview.com.ua/api/admin/chargers/{ocppId}
Content-Type: application/json

{
  "name": "My Wallbox",
  "address": "14 Khreshchatyk St, Kyiv",
  "latitude": 50.4501,
  "longitude": 30.5234,
  "isFastCharger": false,
  "showOnMap": true,
  "maxPowerKw": 7,
  "numberOfConnectors": 1
}
```
