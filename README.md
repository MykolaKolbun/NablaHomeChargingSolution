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
