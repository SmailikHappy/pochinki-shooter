import dotenv from 'dotenv';
import express from 'express';
import fetch from 'node-fetch';
import { fileURLToPath } from 'node:url';

dotenv.config({ path: fileURLToPath(new URL('../.env', import.meta.url)) });

const app = express();
const port = Number(process.env.PORT) || 3001;
const clientId = process.env.VITE_DISCORD_CLIENT_ID;
const clientSecret = process.env.DISCORD_CLIENT_SECRET;

app.use(express.json());

app.get('/api/health', (_request, response) => {
  response.json({ ok: true, time: new Date().toISOString() });
});

app.post('/api/token', async (request, response) => {
  const code = request.body?.code;

  if (!code) {
    return response.status(400).json({ error: 'code is required' });
  }

  if (
    !clientId ||
    !clientSecret ||
    clientId === 'your_client_id_here' ||
    clientSecret === 'your_client_secret_here'
  ) {
    return response.status(500).json({ error: 'Discord credentials are not configured' });
  }

  try {
    const discordResponse = await fetch('https://discord.com/api/oauth2/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        client_id: clientId,
        client_secret: clientSecret,
        grant_type: 'authorization_code',
        code,
      }),
    });

    const data = await discordResponse.json();

    if (!discordResponse.ok) {
      console.error('Discord token exchange failed:', data);
      return response.status(discordResponse.status).json(data);
    }

    return response.json({ access_token: data.access_token });
  } catch (error) {
    console.error('Token exchange error:', error);
    return response.status(500).json({ error: 'token exchange failed' });
  }
});

app.listen(port, () => {
  console.log(`Discord Activity backend: http://localhost:${port}`);
});
