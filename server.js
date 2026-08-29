import express from 'express';
import fetch from 'node-fetch';
import dotenv from 'dotenv';

dotenv.config();

const app = express();
app.use(express.json());
app.use(express.static('public'));

const { DISCORD_CLIENT_ID, DISCORD_CLIENT_SECRET } = process.env;

if (!DISCORD_CLIENT_ID || !DISCORD_CLIENT_SECRET) {
  console.warn(
    'Внимание: DISCORD_CLIENT_ID / DISCORD_CLIENT_SECRET не заданы. Скопируйте .env.example в .env и заполните значения.'
  );
}

// Discord Activity шлёт сюда code, полученный от discordSdk.commands.authorize()
// Этот запрос идёт через Discord proxy: /.proxy/api/token -> сюда
app.post('/api/token', async (req, res) => {
  const { code } = req.body;

  if (!code) {
    return res.status(400).json({ error: 'code is required' });
  }

  try {
    const response = await fetch('https://discord.com/api/oauth2/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        client_id: DISCORD_CLIENT_ID,
        client_secret: DISCORD_CLIENT_SECRET,
        grant_type: 'authorization_code',
        code,
      }),
    });

    const data = await response.json();

    if (!response.ok) {
      console.error('Discord token exchange failed:', data);
      return res.status(response.status).json(data);
    }

    // Клиенту нужен только access_token
    res.json({ access_token: data.access_token });
  } catch (err) {
    console.error('Token exchange error:', err);
    res.status(500).json({ error: 'token exchange failed' });
  }
});

// Простой health-check, полезно при отладке туннеля/URL Mapping
app.get('/api/health', (_req, res) => {
  res.json({ ok: true, time: new Date().toISOString() });
});

const PORT = process.env.PORT || 3001;
app.listen(PORT, () => {
  console.log(`Сервер запущен: http://localhost:${PORT}`);
  console.log('Не забудьте пробросить этот порт через ngrok/cloudflared и указать URL Mapping в Discord Developer Portal.');
});
