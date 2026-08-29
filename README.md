# Pochinki Shooter

Минимальный Discord Activity Hello World с раздельными frontend и backend:

- `client/` — Vite и Discord Embedded App SDK, порт `5173`;
- `server/` — Express endpoint для OAuth token exchange, порт `3001`.

## Локальная настройка

1. Скопируйте `.env.example` в `.env` и заполните значения из Discord Developer Portal:

```env
VITE_DISCORD_CLIENT_ID=your_client_id_here
DISCORD_CLIENT_SECRET=your_client_secret_here
PORT=3001
```

2. Установите зависимости:

```powershell
cd client
npm install

cd ..\server
npm install
```

3. Установите [cloudfare](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/downloads/) (или любой другой прокси) и запустите 

```pwershell
cloudflared tunnel --url http://localhost:5173
```

Врезультате вам выдасться публичный URL:

```powershell
Your quick Tunnel has been created! Visit it at (it may take some time to be reachable):
https://funky-jogging-bunny.trycloudflare.com
```

Этот URL надо скопировать!\
На сайте [Discord dev portal](https://discord.com/developers/applications) выбираете свою активность.\
В левой секции выбираете `Activities -> URL Mappings`.\
На странице вставляете URL в `Root Mapping -> Target`.

4. Запустите backend из:

```powershell
cd server
npm run dev
```

5. В отдельном терминале запустите frontend:

```powershell
cd client
npm run dev
```

Frontend откроется на `http://localhost:5173`. Запросы `/api/*` Vite проксирует на backend.
