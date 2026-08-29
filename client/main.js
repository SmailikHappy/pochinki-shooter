import { DiscordSDK } from '@discord/embedded-app-sdk';
import './style.css';

const clientId = import.meta.env.VITE_DISCORD_CLIENT_ID;
const statusElement = document.querySelector('#status');
const errorElement = document.querySelector('#error');
let currentStage = 'Инициализация';

if (!clientId || clientId === 'your_client_id_here') {
  showError('Укажите VITE_DISCORD_CLIENT_ID в корневом файле .env');
} else {
  const discordSdk = new DiscordSDK(clientId);
  setupDiscord(discordSdk).catch((error) => {
    console.error(error);
    showError(`${currentStage}: ${formatError(error)}`);
  });
}

async function setupDiscord(discordSdk) {
  setStage('Подключение к Discord');
  await waitForDiscordReady(discordSdk);

  setStage('Авторизация в Discord');
  const { code } = await discordSdk.commands.authorize({
    client_id: clientId,
    response_type: 'code',
    state: '',
    prompt: 'none',
    scope: ['identify', 'guilds', 'applications.commands'],
  });

  setStage('Получение токена');
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
  setStage('Проверка пользователя');
  const auth = await discordSdk.commands.authenticate({ access_token: accessToken });

  if (auth == null) {
    throw new Error('Authenticate command failed');
  }

  let channelName = 'Unknown';

  // In a guild, Discord can return the current channel over RPC.
  // DM/GDM channel details require the restricted dm_channels.read scope.
  if (discordSdk.channelId != null && discordSdk.guildId != null) {
    setStage('Получение данных канала');
    const channel = await discordSdk.commands.getChannel({
      channel_id: discordSdk.channelId,
    });

    channelName = channel.name ?? 'Unknown';
  }

  statusElement.textContent = [
    `Привет, ${auth.user.username} 👋`,
    `Channel: ${channelName}`,
    `Guild ID: ${discordSdk.guildId ?? 'DM/GDM'}`,
    `Channel ID: ${discordSdk.channelId ?? 'Unknown'}`,
  ].join('\n');
}

function setStage(stage) {
  currentStage = stage;
  statusElement.textContent = `${stage}…`;
}

function waitForDiscordReady(discordSdk, timeoutMs = 10000) {
  return new Promise((resolve, reject) => {
    const timeoutId = setTimeout(() => {
      const referrerOrigin = getReferrerOrigin();
      const isEmbedded = window.parent !== window;

      reject(
        new Error(
          `Discord SDK не ответил за 10 секунд. Контекст: iframe=${isEmbedded ? 'да' : 'нет'}, platform=${discordSdk.platform}, referrer=${referrerOrigin}. Полностью закройте Discord и запустите Activity через App Launcher снова.`,
        ),
      );
    }, timeoutMs);

    discordSdk.ready().then(
      () => {
        clearTimeout(timeoutId);
        resolve();
      },
      (error) => {
        clearTimeout(timeoutId);
        reject(error);
      },
    );
  });
}

function getReferrerOrigin() {
  if (!document.referrer) {
    return 'пустой';
  }

  try {
    return new URL(document.referrer).origin;
  } catch {
    return 'некорректный';
  }
}

function formatError(error) {
  if (error instanceof Error) {
    return error.message;
  }

  if (typeof error === 'object' && error !== null) {
    const details = JSON.stringify(error, null, 2);
    return details === '{}' ? 'Discord вернул пустой объект ошибки' : details;
  }

  return String(error);
}

function showError(message) {
  statusElement.textContent = 'Ошибка запуска Activity';
  errorElement.textContent = message;
  errorElement.hidden = false;
}
