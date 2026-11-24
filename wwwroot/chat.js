const nickname = window.prompt('Choose a nickname (1-24 characters):');

const state = document.querySelector('#connection-state');
const identity = document.querySelector('#identity');
const messages = document.querySelector('#messages');
const emptyState = document.querySelector('#empty-state');
const input = document.querySelector('#message-input');
const sendButton = document.querySelector('#send-button');
const form = document.querySelector('#message-form');
let socket;

function setConnected(connected) {
  state.classList.toggle('connected', connected);
  state.lastElementChild.textContent = connected ? 'Connected' : 'Disconnected';
  input.disabled = !connected;
  sendButton.disabled = !connected;
  identity.textContent = connected ? `You are ${nickname.trim()}` : 'Not connected';
}

function addSystemMessage(text) {
  emptyState.remove();
  const row = document.createElement('div');
  row.className = 'system-message';
  row.textContent = text;
  messages.append(row);
  messages.scrollTop = messages.scrollHeight;
}

function addMessage(message) {
  emptyState.remove();
  const row = document.createElement('article');
  row.className = 'message';

  const avatar = document.createElement('div');
  avatar.className = 'avatar';
  avatar.textContent = [...message.nickname][0].toUpperCase();

  const body = document.createElement('div');
  const head = document.createElement('div');
  head.className = 'message-head';
  const name = document.createElement('span');
  name.className = 'message-name';
  name.textContent = message.nickname;
  const time = document.createElement('time');
  time.className = 'message-time';
  time.dateTime = message.sentAtUtc;
  time.textContent = new Date(message.sentAtUtc).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  const text = document.createElement('div');
  text.className = 'message-text';
  text.textContent = message.text;

  head.append(name, time);
  body.append(head, text);
  row.append(avatar, body);
  messages.append(row);
  messages.scrollTop = messages.scrollHeight;
}

if (nickname?.trim()) {
  const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:';
  socket = new WebSocket(`${scheme}//${location.host}/chat`);

  socket.addEventListener('open', () => {
    socket.send(JSON.stringify({ type: 'join', nickname: nickname.trim() }));
  });

  socket.addEventListener('message', event => {
    const data = JSON.parse(event.data);
    if (data.type === 'history') {
      data.messages.forEach(addMessage);
    } else if (data.type === 'message') {
      addMessage(data.message);
    } else if (data.type === 'system') {
      addSystemMessage(data.text);
    }
  });

  socket.addEventListener('open', () => setConnected(true));
  socket.addEventListener('close', () => setConnected(false));
  socket.addEventListener('error', () => setConnected(false));
}

form.addEventListener('submit', event => {
  event.preventDefault();
  const text = input.value.trim();
  if (!text || socket?.readyState !== WebSocket.OPEN) return;
  socket.send(JSON.stringify({ type: 'message', text }));
  input.value = '';
  input.focus();
});
