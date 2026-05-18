# OCPP Server

OCPP 1.6 WebSocket server for EV charger communication.

## Charger Connection

Configure your charger's OCPP backend URL to:

```
ws://ocpp.alternatiview.com.ua/ws/{chargePointId}
```

**Example for charger CP-001:**
```
ws://ocpp.alternatiview.com.ua/ws/CP-001
```

The `chargePointId` must match the `OcppId` field set on the station in the EVChargingApi database.
