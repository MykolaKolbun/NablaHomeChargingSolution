# EVHomeApp — Nabla Home mobile app

Expo SDK 57 / React Native 0.86 / React 19.2 / TypeScript 6 (strict). Design system copied from the commercial
Nabla app (`theme/`, `Button`, `Card`, `PowerChart`, `ThemeContext`). Backend: EVHomeAPI
(`docs/api.md`), base URL in `app.json` → `expo.extra.apiBaseUrl`.

## Screens

| Screen | What |
|---|---|
| Login / Register | JWT auth (EVHomeAPI `/api/auth`) |
| My chargers | list with live status; with one charger it opens directly |
| Charger | status → Start → live timer, energy, power, SoC, power chart → Stop (confirm sheet) |
| Add charger | station ID + one-time claim code |
| History | sessions of a charger |
| Profile | dark/light theme, language (uk/en), sign out |

Live data: `src/hooks/useStationLive.ts` — REST is the source of truth, SignalR
(`src/api/stationHub.ts`, `/hubs/charger`) makes it instant; reloads on reconnect and on app
foreground, polls every 30 s while the hub is disconnected.

## Run on a phone (Expo Go, SDK 57)

```bash
npm install
npx expo start --port 8082
```

Scan the QR code with Expo Go. Phone and PC must be on the same network.

Expo Go only runs projects of its own SDK. When Expo Go updates, move the project too:
`npx expo install expo@^<sdk>`, then `npx expo install --fix`, then `npx expo-doctor`.

> Port 8081 (Expo's default) is taken on the dev PC by McAfee Agent (`macmnsvc.exe`) —
> use another port.

Web (`npx expo start --web --port 19006`) renders the UI, but API calls are blocked by CORS
(EVHomeAPI does not allow browser origins) — use a phone for real testing.

## Checks

```bash
npx tsc --noEmit
npx expo-doctor
npx expo export --platform web --output-dir <tmp>
```
