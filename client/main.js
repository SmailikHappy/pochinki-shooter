import { DiscordSDK, Events } from '@discord/embedded-app-sdk';
import './style.css';

const clientId = import.meta.env.VITE_DISCORD_CLIENT_ID;
const statusElement = document.querySelector('#status');
const errorElement = document.querySelector('#error');
const guildAvatarElement = document.querySelector('#guild-avatar');
const statusPanelElement = document.querySelector('#status-panel');
const unityFrameElement = document.querySelector('#unity-frame');

let currentStage = 'Инициализация';
let multiplayerSocket = null;
let multiplayerSession = null;
let reconnectTimer = null;
let reconnectAttempt = 0;
let discordParticipants = [];
let serverSnapshot = createDisconnectedSnapshot('Connecting to multiplayer…');

window.addEventListener('message', handleUnityMessage);
window.addEventListener('beforeunload', () => {
  clearTimeout(reconnectTimer);
  multiplayerSession = null;
  multiplayerSocket?.close(1000, 'Activity closed');
});

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

  serverSnapshot.selfUserId = auth.user.id;
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

  startParticipantTracking(discordSdk).catch((error) => {
    console.warn('Discord participant tracking is unavailable:', error);
  });

  startMultiplayer({
    accessToken,
    instanceId: discordSdk.instanceId,
    selfUserId: auth.user.id,
  });
  launchUnity();
}

async function startParticipantTracking(discordSdk) {
  const updateParticipants = (response) => {
    discordParticipants = Array.isArray(response?.participants) ? response.participants : [];
    postSnapshotToUnity();
  };

  const initialParticipants =
    await discordSdk.commands.getInstanceConnectedParticipants();
  updateParticipants(initialParticipants);
  await discordSdk.subscribe(
    Events.ACTIVITY_INSTANCE_PARTICIPANTS_UPDATE,
    updateParticipants,
  );

  window.addEventListener(
    'beforeunload',
    () => {
      discordSdk
        .unsubscribe(Events.ACTIVITY_INSTANCE_PARTICIPANTS_UPDATE, updateParticipants)
        .catch(() => {});
    },
    { once: true },
  );
}

function startMultiplayer(session) {
  multiplayerSession = session;
  reconnectAttempt = 0;
  openMultiplayerSocket();
}

function openMultiplayerSocket() {
  if (multiplayerSession == null) {
    return;
  }

  clearTimeout(reconnectTimer);
  serverSnapshot = {
    ...serverSnapshot,
    connected: false,
    status: reconnectAttempt === 0 ? 'Connecting to multiplayer…' : 'Reconnecting…',
  };
  postSnapshotToUnity();

  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const socket = new WebSocket(`${protocol}//${window.location.host}/ws`);
  multiplayerSocket = socket;

  socket.addEventListener('open', () => {
    reconnectAttempt = 0;
    socket.send(
      JSON.stringify({
        type: 'join',
        accessToken: multiplayerSession.accessToken,
        instanceId: multiplayerSession.instanceId,
      }),
    );
  });

  socket.addEventListener('message', (event) => {
    let message;

    try {
      message = JSON.parse(event.data);
    } catch {
      return;
    }

    if (message.type === 'snapshot' && message.payload != null) {
      serverSnapshot = normalizeSnapshot(message.payload);
      postSnapshotToUnity();
      return;
    }

    if (message.type === 'error') {
      serverSnapshot = {
        ...serverSnapshot,
        connected: false,
        status: String(message.message || 'Multiplayer error'),
      };
      postSnapshotToUnity();
    }
  });

  socket.addEventListener('close', (event) => {
    if (multiplayerSocket !== socket || multiplayerSession == null) {
      return;
    }

    serverSnapshot = {
      ...serverSnapshot,
      connected: false,
      status: event.code === 4001 ? 'Opened in another window' : 'Connection lost',
    };
    postSnapshotToUnity();

    if (event.code === 4001 || event.code === 4003) {
      return;
    }

    const delay = Math.min(5000, 500 * 2 ** reconnectAttempt);
    reconnectAttempt++;
    reconnectTimer = setTimeout(openMultiplayerSocket, delay);
  });

  socket.addEventListener('error', () => {
    // The close handler reports a useful state and starts reconnection.
  });
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
    postSnapshotToUnity();
    return;
  }

  if (event.data.type !== 'pochinki:unity-input') {
    return;
  }

  if (multiplayerSocket?.readyState !== WebSocket.OPEN) {
    return;
  }

  const payload = event.data.payload;

  if (payload == null || typeof payload !== 'object') {
    return;
  }

  multiplayerSocket.send(JSON.stringify({ type: 'input', payload }));
}

function postSnapshotToUnity() {
  if (!unityFrameElement.contentWindow) {
    return;
  }

  unityFrameElement.contentWindow.postMessage(
    {
      type: 'pochinki:multiplayer-snapshot',
      payload: mergeDiscordParticipants(serverSnapshot),
    },
    window.location.origin,
  );
}

function mergeDiscordParticipants(snapshot) {
  const participantsById = new Map(
    snapshot.participants.map((participant) => [participant.userId, participant]),
  );

  for (const user of discordParticipants) {
    const current = participantsById.get(user.id);
    const username = user.nickname || user.global_name || user.username || 'Unknown';

    if (current != null) {
      participantsById.set(user.id, {
        ...current,
        username,
        avatarUrl: buildDiscordAvatarUrl(user) || current.avatarUrl,
      });
      continue;
    }

    participantsById.set(user.id, {
      userId: user.id,
      username,
      avatarUrl: buildDiscordAvatarUrl(user),
      lastEvent: 'Waiting for multiplayer data',
      mouseX: 0,
      mouseY: 0,
      eventCount: 0,
      updatedAt: 0,
    });
  }

  const participants = [...participantsById.values()];

  return {
    ...snapshot,
    status: snapshot.connected
      ? `Synced: ${participants.length}`
      : snapshot.status,
    participants,
  };
}

function normalizeSnapshot(value) {
  return {
    connected: value.connected === true,
    status: typeof value.status === 'string' ? value.status : 'Multiplayer',
    selfUserId:
      typeof value.selfUserId === 'string'
        ? value.selfUserId
        : serverSnapshot.selfUserId,
    participants: Array.isArray(value.participants)
      ? value.participants.map((participant) => ({
          userId: String(participant.userId || ''),
          username: String(participant.username || 'Unknown'),
          avatarUrl: String(participant.avatarUrl || ''),
          lastEvent: String(participant.lastEvent || ''),
          mouseX: Number(participant.mouseX) || 0,
          mouseY: Number(participant.mouseY) || 0,
          eventCount: Number(participant.eventCount) || 0,
          updatedAt: Number(participant.updatedAt) || 0,
        }))
      : [],
  };
}

function createDisconnectedSnapshot(status) {
  return {
    connected: false,
    status,
    selfUserId: '',
    participants: [],
  };
}

function buildDiscordAvatarUrl(user) {
  if (!user?.id || !user?.avatar) {
    return '';
  }

  return `https://cdn.discordapp.com/avatars/${user.id}/${user.avatar}.webp?size=64`;
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
    return details === '{}' ? 'Discord вернул пустой объект ошибки' : details;
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
