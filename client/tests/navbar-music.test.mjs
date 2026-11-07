import test from 'node:test';
import assert from 'node:assert/strict';
import { DEFAULT_NAVBAR_TRACKS, extractYouTubeId, navbarMusicActor, navbarMusicActorIsCurrent, navbarMusicUrl, navbarMusicVolume, normalizeNavbarPlaylist, saveNavbarPlaylist } from '../src/components/navbarMusicModel.ts';

const managed = '/uploads/invitation-audio/c8e62673872e40a09bed56b3d42c8ebf/0123456789abcdef0123456789abcdef.mp3';
const track = () => ({ id: 'owned-track', title: ' First dance ', type: 'audio', url: managed, isCustom: true });
const session = { user: { id: 'admin-a', role: 'SuperAdmin' }, token: 'synthetic-token', validated: true, sessionRevision: 4 };

test('Music references accept only curated managed audio or direct HTTPS audio without credentials', () => {
  assert.equal(navbarMusicUrl(managed), managed);
  assert.equal(navbarMusicUrl('https://example.invalid/song.mp3?version=2'), 'https://example.invalid/song.mp3?version=2');
  assert.equal(navbarMusicUrl(DEFAULT_NAVBAR_TRACKS[1].url), DEFAULT_NAVBAR_TRACKS[1].url);
  for (const url of ['http://example.invalid/song.mp3', 'javascript:alert(1)', 'blob:preview', 'data:audio/mpeg;base64,a', '//example.invalid/song.mp3', 'https://user:pass@example.invalid/song.mp3', 'https://example.invalid:8443/song.mp3', 'https://example.invalid/page.html', 'https://example.invalid/%252e%252e/song.mp3', 'https://example.invalid/a\\song.mp3', managed.replace('c8e62673872e40a09bed56b3d42c8ebf', 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'), managed.replace('.mp3', '.m4a')])
    assert.throws(() => navbarMusicUrl(url), url);
});

test('Playlist validates all metadata and does not trust local-shaped payloads or unknown properties', () => {
  const cleaned = normalizeNavbarPlaylist([{ ...track(), unexpected: 'discard' }, { ...DEFAULT_NAVBAR_TRACKS[0], isCustom: true }]);
  assert.equal(cleaned[0].title, 'First dance');
  assert.equal(Object.hasOwn(cleaned[0], 'unexpected'), false);
  assert.equal(cleaned[1].isCustom, false);
  assert.equal(cleaned[0].hidden, false);

  const withHidden = normalizeNavbarPlaylist([{ ...track(), hidden: true }]);
  assert.equal(withHidden[0].hidden, true);
  assert.deepEqual(normalizeNavbarPlaylist([]), []);
  for (const value of [null, {}, [track(), track()], [{ ...track(), id: 'bad/id' }], [{ ...track(), title: ' ' }], [{ ...track(), title: 'a'.repeat(151) }], [{ ...track(), title: 'bad\u0000title' }], [{ ...track(), type: 'synth' }], [{ ...DEFAULT_NAVBAR_TRACKS[0], url: managed }], Array.from({ length: 21 }, (_, i) => ({ ...track(), id: `item-${i}` }))])
    assert.throws(() => normalizeNavbarPlaylist(value));
});

test('YouTube link and video ID parsing safely extracts valid video IDs and normalizes tracks', () => {
  assert.equal(extractYouTubeId('https://www.youtube.com/watch?v=dQw4w9WgXcQ'), 'dQw4w9WgXcQ');
  assert.equal(extractYouTubeId('https://youtu.be/dQw4w9WgXcQ'), 'dQw4w9WgXcQ');
  assert.equal(extractYouTubeId('https://www.youtube.com/shorts/dQw4w9WgXcQ'), 'dQw4w9WgXcQ');
  assert.equal(extractYouTubeId('dQw4w9WgXcQ'), 'dQw4w9WgXcQ');
  assert.equal(extractYouTubeId('https://malicious.example.com/watch?v=dQw4w9WgXcQ'), null);
  assert.equal(extractYouTubeId('javascript:alert(1)'), null);

  const ytTrack = { id: 'yt-1', title: 'Until I Found You', type: 'youtube', url: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ', isCustom: true };
  const normalized = normalizeNavbarPlaylist([ytTrack]);
  assert.equal(normalized[0].youtubeId, 'dQw4w9WgXcQ');
  assert.equal(normalized[0].type, 'youtube');
  assert.equal(normalized[0].title, 'Until I Found You');
  assert.equal(normalized[0].isCustom, true);
});

test('Music management requires a validated staff actor and binds token, account, role and revision', () => {
  const actor = navbarMusicActor(session);
  assert.ok(actor);
  assert.equal(navbarMusicActorIsCurrent(actor, session), true);
  for (const changed of [{ ...session, validated: false }, { ...session, token: null }, { ...session, token: 'replacement' }, { ...session, sessionRevision: 5 }, { ...session, user: { id: 'admin-b', role: 'SuperAdmin' } }, { ...session, user: { id: 'admin-a', role: 'Moderator' } }, { ...session, user: { id: 'admin-a', role: 'Customer' } }])
    assert.equal(navbarMusicActorIsCurrent(actor, changed), false);
  assert.equal(navbarMusicActor({ ...session, validated: false }), null);
  assert.equal(navbarMusicActor({ ...session, user: { id: 'customer', role: 'Customer' } }), null);
});

test('Playlist save returns only the server acknowledgement; rejection and stale sessions cannot commit', async () => {
  const controller = new AbortController();
  let current = true, sends = 0;
  const options = { signal: controller.signal, isCurrent: () => current, send: async () => { sends++; return { success: true, tracks: [{ ...track(), title: 'Server title' }] }; } };
  assert.equal((await saveNavbarPlaylist([track()], options))[0].title, 'Server title');
  await assert.rejects(saveNavbarPlaylist([track()], { ...options, send: async () => { throw new Error('Save failed'); } }), /Save failed/);
  for (const response of [{ success: true }, { success: false, tracks: [track()] }, { success: true, tracks: [{ ...track(), url: 'blob:preview' }] }])
    await assert.rejects(saveNavbarPlaylist([track()], { ...options, send: async () => response }));
  await assert.rejects(saveNavbarPlaylist([track()], { ...options, send: async () => { current = false; return { success: true, tracks: [track()] }; } }));
  await assert.rejects(saveNavbarPlaylist([track()], options));
  assert.equal(sends, 1);
  current = true; controller.abort();
  await assert.rejects(saveNavbarPlaylist([track()], options));
  assert.equal(sends, 1);
});

test('Volume preferences remain finite and bounded even when storage is corrupt', () => {
  for (const value of ['NaN', 'Infinity', '-Infinity', '', null, undefined, {}, NaN, Infinity]) assert.equal(navbarMusicVolume(value), .45);
  assert.equal(navbarMusicVolume('0'), 0);
  assert.equal(navbarMusicVolume('-2'), 0);
  assert.equal(navbarMusicVolume('2'), 1);
  assert.equal(navbarMusicVolume('.75'), .75);
  assert.match(DEFAULT_NAVBAR_TRACKS[1].title, /Âm thanh/);
  assert.doesNotMatch(DEFAULT_NAVBAR_TRACKS[1].title, /Thousand Years/);
});
