import dotenv from 'dotenv';
import express from 'express';
import fetch from 'node-fetch';
import { createServer } from 'node:http';
import { fileURLToPath } from 'node:url';
import { WebSocket, WebSocketServer } from 'ws';

dotenv.config({ path: fileURLToPath(new URL('../.env', import.meta.url)) });

const app = express();
const server = createServer(app);
const socketServer = new WebSocketServer({
  noServer: true,
  clientTracking: false,
  maxPayload: 8 * 1024,
});
const port = Number(process.env.PORT) || 3001;
const clientId = process.env.VITE_DISCORD_CLIENT_ID;
const clientSecret = process.env.DISCORD_CLIENT_SECRET;
const rooms = new Map();
const clients = new Map();

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

server.on('upgrade', (request, socket, head) => {
  const requestUrl = new URL(request.url ?? '/', 'http://localhost');

  if (requestUrl.pathname !== '/ws') {
    socket.destroy();
    return;
  }

  socketServer.handleUpgrade(request, socket, head, (webSocket) => {
    socketServer.emit('connection', webSocket);
  });
});

socketServer.on('connection', (socket) => {
  socket.isAlive = true;
  socket.on('pong', () => {
    socket.isAlive = true;
  });

  const authenticationTimeout = setTimeout(() => {
    sendError(socket, 'Discord authentication timed out');
    socket.close(4000, 'Authentication timeout');
  }, 10_000);

  let authenticating = false;

  socket.on('message', async (rawMessage) => {
    const message = parseMessage(rawMessage);

    if (message == null) {
      sendError(socket, 'Invalid JSON message');
      return;
    }

    const currentClient = clients.get(socket);

    if (currentClient == null) {
      if (message.type !== 'join' || authenticating) {
        return;
      }

      authenticating = true;

      try {
        await joinRoom(socket, message);
        clearTimeout(authenticationTimeout);
      } catch (error) {
        console.error('WebSocket join failed:', error);
        sendError(socket, error instanceof Error ? error.message : 'Unable to join room');
        socket.close(4003, 'Authentication failed');
      }

      return;
    }

    if (message.type === 'input') {
      updateInput(currentClient, message.payload);
    }
  });

  socket.on('close', () => {
    clearTimeout(authenticationTimeout);
    leaveRoom(socket);
  });

  socket.on('error', (error) => {
    console.error('WebSocket error:', error.message);
  });
});

const heartbeat = setInterval(() => {
  for (const socket of clients.keys()) {
    if (!socket.isAlive) {
      socket.terminate();
      continue;
    }

    socket.isAlive = false;
    socket.ping();
  }
}, 30_000);

heartbeat.unref();

async function joinRoom(socket, message) {
  const accessToken = typeof message.accessToken === 'string' ? message.accessToken : '';
  const instanceId = typeof message.instanceId === 'string' ? message.instanceId : '';

  if (!accessToken || !isValidInstanceId(instanceId)) {
    throw new Error('Invalid Discord session');
  }

  const discordResponse = await fetch('https://discord.com/api/v10/users/@me', {
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!discordResponse.ok) {
    throw new Error('Discord user verification failed');
  }

  const user = await discordResponse.json();

  if (typeof user.id !== 'string' || typeof user.username !== 'string') {
    throw new Error('Discord returned an invalid user');
  }

  let room = rooms.get(instanceId);

  if (room == null) {
    room = new Map();
    rooms.set(instanceId, room);
  }

  const previousConnection = room.get(user.id);

  if (previousConnection != null && previousConnection.socket !== socket) {
    clients.delete(previousConnection.socket);
    previousConnection.socket.close(4001, 'Reconnected from another window');
  }

  const participant = {
    socket,
    roomId: instanceId,
    userId: user.id,
    username: cleanText(user.global_name || user.username, 48),
    avatarUrl: buildAvatarUrl(user),
    lastEvent: 'Connected to multiplayer',
    mouseX: 0,
    mouseY: 0,
    eventCount: 0,
    updatedAt: Date.now(),
    rateWindowStartedAt: Date.now(),
    messagesInRateWindow: 0,
  };

  room.set(user.id, participant);
  clients.set(socket, participant);
  broadcastRoom(instanceId);
}

function updateInput(participant, payload) {
  if (payload == null || typeof payload !== 'object' || !consumeRateLimit(participant)) {
    return;
  }

  participant.lastEvent = cleanText(payload.lastEvent, 120) || participant.lastEvent;
  participant.mouseX = clampNumber(payload.mouseX, 0, 1);
  participant.mouseY = clampNumber(payload.mouseY, 0, 1);
  participant.eventCount = clampInteger(payload.eventCount, 0, 1_000_000_000);
  participant.updatedAt = Date.now();
  broadcastRoom(participant.roomId);
}

function consumeRateLimit(participant) {
  const now = Date.now();

  if (now - participant.rateWindowStartedAt >= 1000) {
    participant.rateWindowStartedAt = now;
    participant.messagesInRateWindow = 0;
  }

  participant.messagesInRateWindow++;
  return participant.messagesInRateWindow <= 30;
}

function leaveRoom(socket) {
  const participant = clients.get(socket);

  if (participant == null) {
    return;
  }

  clients.delete(socket);
  const room = rooms.get(participant.roomId);

  if (room == null || room.get(participant.userId)?.socket !== socket) {
    return;
  }

  room.delete(participant.userId);

  if (room.size === 0) {
    rooms.delete(participant.roomId);
    return;
  }

  broadcastRoom(participant.roomId);
}

function broadcastRoom(roomId) {
  const room = rooms.get(roomId);

  if (room == null) {
    return;
  }

  const participants = [...room.values()].map((participant) => ({
    userId: participant.userId,
    username: participant.username,
    avatarUrl: participant.avatarUrl,
    lastEvent: participant.lastEvent,
    mouseX: participant.mouseX,
    mouseY: participant.mouseY,
    eventCount: participant.eventCount,
    updatedAt: participant.updatedAt,
  }));

  for (const participant of room.values()) {
    sendJson(participant.socket, {
      type: 'snapshot',
      payload: {
        connected: true,
        status: `Synced: ${participants.length}`,
        selfUserId: participant.userId,
        participants,
      },
    });
  }
}

function parseMessage(rawMessage) {
  try {
    return JSON.parse(rawMessage.toString());
  } catch {
    return null;
  }
}

function sendError(socket, message) {
  sendJson(socket, { type: 'error', message });
}

function sendJson(socket, value) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(value));
  }
}

function isValidInstanceId(value) {
  return value.length >= 1 && value.length <= 256 && /^[a-zA-Z0-9:_-]+$/.test(value);
}

function cleanText(value, maxLength) {
  return typeof value === 'string'
    ? value.replace(/[\u0000-\u001f\u007f]/g, ' ').trim().slice(0, maxLength)
    : '';
}

function clampNumber(value, min, max) {
  const number = Number(value);
  return Number.isFinite(number) ? Math.min(max, Math.max(min, number)) : min;
}

function clampInteger(value, min, max) {
  return Math.round(clampNumber(value, min, max));
}

function buildAvatarUrl(user) {
  if (typeof user.avatar !== 'string' || !user.avatar) {
    return '';
  }

  return `https://cdn.discordapp.com/avatars/${user.id}/${user.avatar}.webp?size=64`;
}

server.listen(port, () => {
  console.log(`Discord Activity backend: http://localhost:${port}`);
  console.log(`Multiplayer WebSocket: ws://localhost:${port}/ws`);
});
