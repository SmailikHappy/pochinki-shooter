import { DiscordSDK } from '@discord/embedded-app-sdk';
import './style.css';

const clientId = import.meta.env.VITE_DISCORD_CLIENT_ID;
const statusElement = document.querySelector('#status');
const errorElement = document.querySelector('#error');
const guildAvatarElement = document.querySelector('#guild-avatar');
const statusPanelElement = document.querySelector('#status-panel');
const unityFrameElement = document.querySelector('#unity-frame');

let currentStage = 'Инициализация';
let discordSession = null;

window.addEventListener('message', handleUnityMessage);

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
  if (discordSdk.channelId != null && discordSdk.guildId != null) {
    setStage('Получение данных канала');
    const channel = await discordSdk.commands.getChannel({
      channel_id: discordSdk.channelId,
    });
    channelName = channel.name ?? 'Unknown';
  }

  let currentGuild = null;
  if (discordSdk.guildId != null) {
    setStage('Получение данных сервера');
    currentGuild = await fetchCurrentGuild(accessToken, discordSdk.guildId);
  }

  showGuildAvatar(currentGuild);
  statusElement.textContent = [
    `Привет, ${auth.user.username} 👋`,
    `Channel: ${channelName}`,
    `Server: ${currentGuild?.name ?? 'Unknown'}`,
    `Guild ID: ${discordSdk.guildId ?? 'DM/GDM'}`,
    `Channel ID: ${discordSdk.channelId ?? 'Unknown'}`,
  ].join('\n');

  discordSession = {
    selfUserId: auth.user.id,
    selfUsername: auth.user.username,
    instanceId: discordSdk.instanceId,
  };

  postDiscordSessionToUnity();
  launchUnity();
}

function handleUnityMessage(event) {
  if (
    event.source !== unityFrameElement.contentWindow ||
    event.origin !== window.location.origin ||
    event.data == null
  ) {
    return;
  }

  if (event.data.type === 'pochinki:unity-ready') {
    postDiscordSessionToUnity();
  }
}

function postDiscordSessionToUnity() {
  if (!discordSession || !unityFrameElement.contentWindow) {
    return;
  }

  unityFrameElement.contentWindow.postMessage(
    {
      type: 'pochinki:discord-session',
      payload: discordSession,
    },
    window.location.origin,
  );
}

function launchUnity() {
  unityFrameElement.addEventListener(
    'load',
    () => {
      statusPanelElement.hidden = true;
      unityFrameElement.hidden = false;
    },
    { once: true },
  );

  unityFrameElement.src = '/unity-build/index.html';
}

async function fetchCurrentGuild(accessToken, guildId) {
  const response = await fetch('https://discord.com/api/v10/users/@me/guilds', {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
    },
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(`Guild request failed (${response.status}): ${details}`);
  }

  const guilds = await response.json();
  if (!Array.isArray(guilds)) {
    throw new Error('Discord returned an invalid guild list');
  }

  return guilds.find((guild) => guild.id === guildId) ?? null;
}

function showGuildAvatar(guild) {
  if (!guild?.icon) {
    guildAvatarElement.hidden = true;
    return;
  }

  guildAvatarElement.src = `https://cdn.discordapp.com/icons/${guild.id}/${guild.icon}.webp?size=128`;
  guildAvatarElement.alt = `Аватар сервера ${guild.name}`;
  guildAvatarElement.hidden = false;
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
    return details === '{}'
      ? 'Discord вернул пустой объект ошибки'
      : details;
  }

  return String(error);
}

function showError(message) {
  unityFrameElement.hidden = true;
  statusPanelElement.hidden = false;
  statusElement.textContent = 'Ошибка запуска Activity';
  errorElement.textContent = message;
  errorElement.hidden = false;
}
