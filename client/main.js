import { DiscordSDK } from '@discord/embedded-app-sdk';
import './style.css';

const clientId = import.meta.env.VITE_DISCORD_CLIENT_ID;
const statusElement = document.querySelector('#status');
const errorElement = document.querySelector('#error');

if (!clientId || clientId === 'your_client_id_here') {
  showError('Укажите VITE_DISCORD_CLIENT_ID в корневом файле .env');
} else {
  const discordSdk = new DiscordSDK(clientId);
  setupDiscord(discordSdk).catch((error) => {
    console.error(error);
    showError(error instanceof Error ? error.message : String(error));
  });
}

async function setupDiscord(discordSdk) {
  await discordSdk.ready();

  const { code } = await discordSdk.commands.authorize({
    client_id: clientId,
    response_type: 'code',
    prompt: 'none',
    scope: ['identify'],
  });

  const response = await fetch('/api/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code }),
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(`Token exchange failed (${response.status}): ${details}`);
  }

  const { access_token: accessToken } = await response.json();
  const auth = await discordSdk.commands.authenticate({ access_token: accessToken });

  statusElement.textContent = `Привет, ${auth.user.username} 👋`;
}

function showError(message) {
  statusElement.textContent = 'Ошибка запуска Activity';
  errorElement.textContent = message;
  errorElement.hidden = false;
}
