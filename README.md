# Pochinki Shooter

Минимальный Discord Activity Hello World с раздельными frontend и backend:

- `client/` — Vite и Discord Embedded App SDK, порт `5173`;
- `server/` — Express endpoint для OAuth token exchange, порт `3001`.

## Локальная настройка

Скопируйте `.env.example` в `.env` и заполните значения из Discord Developer Portal:

```env
VITE_DISCORD_CLIENT_ID=your_client_id_here
DISCORD_CLIENT_SECRET=your_client_secret_here
PORT=3001
```

Установите зависимости:

```powershell
cd client
npm install

cd ..\server
npm install
```

Запустите backend:

```powershell
cd server
npm run dev
```

В отдельном терминале запустите frontend:

```powershell
cd client
npm run dev
```

Frontend откроется на `http://localhost:5173`. Запросы `/api/*` Vite проксирует на backend.
