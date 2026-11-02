// Local-only companion to Verify-AccountSecurity.ps1. No host accounts, real
// customer records, global TLS overrides, or production credentials are used.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import https from 'node:https';
import { createRequire } from 'node:module';
import { setTimeout as delay } from 'node:timers/promises';

const clientRequire = createRequire(new URL('../client/package.json', import.meta.url));
const signalR = clientRequire('@microsoft/signalr');
const signalRRequire = createRequire(clientRequire.resolve('@microsoft/signalr'));
const WebSocket = signalRRequire('ws');
const fixture = JSON.parse(readFileSync(process.argv[2], 'utf8'));
assert.equal(fixture.baseUrl, 'https://127.0.0.1:5011', 'Only disposable loopback HTTPS is allowed.');
const base = new URL(fixture.baseUrl);
const sockets = [];
let step = 'initialization';

// The certificate bypass is confined to this exact loopback endpoint, not a
// process-wide NODE_TLS_REJECT_UNAUTHORIZED setting or a reusable browser.
class LoopbackWebSocket extends WebSocket {
  constructor(url, protocols, options) {
    const target = new URL(url);
    assert.equal(target.protocol, 'wss:'); assert.equal(target.hostname, '127.0.0.1'); assert.equal(target.port, '5011');
    super(url, protocols, { ...options, rejectUnauthorized: false });
  }
}

async function request(path, { token, body, method = body === undefined ? 'GET' : 'POST' } = {}) {
  assert.ok(path.startsWith('/api/'));
  const payload = body === undefined ? undefined : JSON.stringify(body);
  return await new Promise((resolve, reject) => {
    const req = https.request({
      protocol: base.protocol, hostname: base.hostname, port: base.port, path, method,
      rejectUnauthorized: false, timeout: 15000,
      headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(payload ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(payload) } : {}) },
    }, response => {
      const chunks = [];
      response.on('data', chunk => chunks.push(chunk));
      response.on('end', () => {
        const text = Buffer.concat(chunks).toString('utf8');
        let json; try { json = text ? JSON.parse(text) : null; } catch { json = null; }
        resolve({ status: response.statusCode, json });
      });
    });
    req.on('timeout', () => req.destroy(new Error('Loopback QA request timed out.')));
    req.on('error', reject); if (payload) req.write(payload); req.end();
  });
}

async function connect(token) {
  const received = [];
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${fixture.baseUrl}/hubs/chat`, { transport: signalR.HttpTransportType.WebSockets,
      skipNegotiation: true, accessTokenFactory: () => token, WebSocket: LoopbackWebSocket })
    .configureLogging(signalR.LogLevel.None).build();
  sockets.push(connection);
  connection.on('ReceiveMessage', message => received.push(message));
  await connection.start();
  return { connection, received };
}

async function expectDenied(action) {
  let failure;
  try { await action(); } catch (error) { failure = error; }
  assert.ok(failure, 'Expected the operation to be denied.');
  return failure.message;
}

async function expectReceived(clients, marker) {
  for (let attempt = 0; attempt < 40; attempt++) {
    if (clients.every(client => client.received.some(message => message.content === marker))) return;
    await delay(100);
  }
  assert.fail('An authorized current socket did not receive its realtime message.');
}

async function postMessage(marker) {
  const response = await request(`/api/leads/${fixture.leadId}/messages`, {
    token: fixture.vendor.token, body: { content: marker, isQuote: false },
  });
  assert.equal(response.status, 200, 'Authorized REST chat send failed.');
}

try {
  step = 'connect real participant, moderator and unrelated WebSockets';
  const [couple, vendor, moderator, stranger] = await Promise.all([
    connect(fixture.customer.token), connect(fixture.vendor.token),
    connect(fixture.moderator.token), connect(fixture.otherCustomer.token),
  ]);
  await couple.connection.invoke('JoinLeadRoom', `{${fixture.leadId.toUpperCase()}}`);
  await vendor.connection.invoke('JoinLeadRoom', fixture.leadId.replaceAll('-', ''));
  await moderator.connection.invoke('JoinLeadRoom', fixture.leadId);

  step = 'deny unrelated room access without an existence leak';
  const known = await expectDenied(() => stranger.connection.invoke('JoinLeadRoom', fixture.leadId));
  const unknown = await expectDenied(() => stranger.connection.invoke('JoinLeadRoom', '11111111-1111-1111-1111-111111111111'));
  const invalid = await expectDenied(() => stranger.connection.invoke('JoinLeadRoom', 'not-a-guid'));
  assert.equal(known, unknown); assert.equal(known, invalid);
  await expectDenied(() => stranger.connection.invoke('LeaveLeadRoom', fixture.leadId));
  await expectDenied(() => stranger.connection.invoke('SendMessage', fixture.leadId, 'forbidden', false, null, null));
  await expectDenied(() => moderator.connection.invoke('SendMessage', fixture.leadId, 'staff cannot send', false, null, null));

  step = 'REST broadcasts reach authorized canonical groups only';
  const baseline = `QA-baseline-${Date.now()}`;
  await postMessage(baseline); await expectReceived([couple, vendor, moderator], baseline);
  await delay(200); assert.equal(stranger.received.length, 0);
  assert.equal((await request(`/api/leads/${fixture.leadId}/messages`, { token: fixture.moderator.token })).status, 200);
  const strangerHistory = await request(`/api/leads/${fixture.leadId}/messages`, { token: fixture.otherCustomer.token });
  assert.ok([400, 403, 404].includes(strangerHistory.status), 'Unrelated customer read private HTTP chat history.');
  assert.ok(!JSON.stringify(strangerHistory.json).includes(baseline));

  step = 'password change requires authentication and the correct current password';
  const passwordBody = { currentPassword: fixture.password, newPassword: fixture.nextPassword, confirmPassword: fixture.nextPassword };
  assert.equal((await request('/api/auth/change-password', { body: passwordBody })).status, 401);
  assert.equal((await request('/api/auth/change-password', { token: fixture.customer.token,
    body: { ...passwordBody, currentPassword: 'not-the-current-QA-password' } })).status, 400);
  assert.equal((await request('/api/auth/me', { token: fixture.customer.token })).status, 200);

  step = 'password change revokes both devices without changing actors';
  assert.equal((await request('/api/auth/change-password', { token: fixture.customer.token,
    body: { ...passwordBody, userId: fixture.vendor.user.id, role: 'SuperAdmin' } })).status, 204);
  assert.equal((await request('/api/auth/me', { token: fixture.customer.token })).status, 401);
  assert.equal((await request('/api/auth/me', { token: fixture.secondDeviceToken })).status, 401);
  assert.equal((await request('/api/auth/login', { body: { email: fixture.customer.user.email, password: fixture.password } })).status, 400);
  const login = await request('/api/auth/login', { body: { email: fixture.customer.user.email, password: fixture.nextPassword } });
  assert.equal(login.status, 200); assert.equal(login.json.user.id, fixture.customer.user.id);
  assert.equal(login.json.user.role, 'Customer');
  assert.equal((await request('/api/auth/me', { token: login.json.token })).status, 200);

  step = 'revoke an already joined live WebSocket after password change';
  await expectDenied(() => couple.connection.invoke('JoinLeadRoom', fixture.leadId));
  await expectDenied(() => couple.connection.invoke('LeaveLeadRoom', fixture.leadId));
  await expectDenied(() => couple.connection.invoke('SendMessage', fixture.leadId, 'revoked socket', false, null, null));
  const afterChange = `QA-after-change-${Date.now()}`;
  await postMessage(afterChange); await expectReceived([vendor, moderator], afterChange);
  await delay(300);
  assert.ok(!couple.received.some(message => message.content === afterChange), 'Revoked live socket received private content.');
  assert.equal(stranger.received.length, 0);
  await expectDenied(() => connect(fixture.customer.token));

  step = 'fresh login rejoins and actual Hub sends reach current sessions only';
  const fresh = await connect(login.json.token);
  await fresh.connection.invoke('JoinLeadRoom', fixture.leadId);
  const hubMarker = `QA-current-hub-${Date.now()}`;
  await vendor.connection.invoke('SendMessage', fixture.leadId, hubMarker, false, null, null);
  await expectReceived([fresh, vendor, moderator], hubMarker); await delay(300);
  assert.ok(!couple.received.some(message => message.content === hubMarker));
  assert.equal(stranger.received.length, 0);

  step = 'customer contact update replaces only its own session and excludes vendor actors';
  assert.equal((await request('/api/auth/contact', { method: 'PATCH', token: fixture.vendor.token,
    body: { phoneNumber: '0912345678' } })).status, 403);
  const contact = await request('/api/auth/contact', { method: 'PATCH', token: fixture.otherCustomer.token,
    body: { phoneNumber: '0912345678', userId: fixture.vendor.user.id, role: 'SuperAdmin' } });
  assert.equal(contact.status, 200); assert.equal(contact.json.user.id, fixture.otherCustomer.user.id);
  assert.equal(contact.json.user.role, 'Customer'); assert.equal(contact.json.user.phoneNumber, '0912345678');
  assert.equal((await request('/api/auth/me', { token: fixture.otherCustomer.token })).status, 401);
  const newContact = await request('/api/auth/me', { token: contact.json.token });
  assert.equal(newContact.status, 200); assert.equal(newContact.json.phoneNumber, '0912345678');
  assert.equal(newContact.json.id, fixture.otherCustomer.user.id); assert.equal(newContact.json.role, 'Customer');
  console.log('PASS: real HTTPS registration/role isolation; wrong-password safety; password change revokes two JWTs; old password fails; new login succeeds; unrelated/staff-send chat denied; canonical room IDs; authorized REST and Hub realtime delivery; already-open revoked WebSocket receives no future private messages; customer-only contact update returns a valid replacement session and revokes its old token.');
} catch (error) {
  const detail = error instanceof assert.AssertionError ? error.message : (error.name || 'Error');
  console.error(`FAIL account/security smoke at step: ${step}. ${detail}`);
  process.exitCode = 1;
} finally {
  await Promise.allSettled(sockets.map(connection => connection.stop()));
}
