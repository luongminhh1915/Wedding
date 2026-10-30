import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import ts from "typescript";
import { invitationAudioFileError, invitationAudioSrc, invitationMusicSource } from "../src/features/journey/invitations/invitationMusic.ts";

const videoId = "M7lc1UVf-VE";
const upload = "/uploads/invitation-audio/0123456789abcdef0123456789abcdef/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp3";
test("music source derives upload/none/legacy without changing persisted paths", () => {
  assert.deepEqual(invitationMusicSource(null), { kind: "none" });
  assert.deepEqual(invitationMusicSource("  "), { kind: "none" });
  assert.deepEqual(invitationMusicSource(upload), { kind: "upload", url: upload });
  for (const url of ["http://old.example.test/song.mp3", "https://music.example.test/audio?id=2"]) assert.deepEqual(invitationMusicSource(url), { kind: "legacy", url });
  assert.equal(invitationAudioSrc(upload, "https://api.example.test/"), `https://api.example.test${upload}`);
  assert.equal(invitationAudioSrc(upload, ""), upload);
  assert.equal(invitationAudioSrc("https://old.example.test/song.mp3", "https://api.example.test"), "https://old.example.test/song.mp3");
});
test("YouTube allowlist accepts single video routes and canonicalizes to stable URL", () => {
  for (const url of [
    `https://www.youtube.com/watch?v=${videoId}&t=10`, `https://youtube.com/watch?v=${videoId}`,
    `https://m.youtube.com/shorts/${videoId}`, `https://music.youtube.com/watch?v=${videoId}`,
    `https://youtu.be/${videoId}?si=old`, `https://www.youtu.be/${videoId}`, `https://youtube.com/live/${videoId}`,
    `https://www.youtube-nocookie.com/embed/${videoId}`, `https://www.youtube.com/embed/${videoId}`,
  ]) assert.deepEqual(invitationMusicSource(url), { kind: "youtube", url: `https://www.youtube.com/watch?v=${videoId}`, videoId }, url);
});
test("malformed YouTube/unsafe URLs never fall back to iframe or audio", () => {
  for (const url of ["javascript:alert(1)", "data:audio/mpeg,abc", "blob:https://example.test/temp", "//example.test/song.mp3",
    "/uploads/invitation-audio/../../secret.mp3", upload.replace(".mp3", ".html"), "https://user:password@example.test/song.mp3",
    `http://youtube.com/watch?v=${videoId}`, `https://youtube.com:444/watch?v=${videoId}`,
    `https://youtube.com/watch?v=${videoId}&v=aaaaaaaaaaa`, `https://youtu.be/${videoId}/extra`,
    `https://youtube.com/embed/${videoId}/`, `https://youtube.com/playlist?list=abc`, `https://youtube.com/watch?v=short`,
    `https://youtube.com.evil.test/watch?v=${videoId}`, `https://notyoutube.com/watch?v=${videoId}`,
    `https://youtube-nocookie.com/watch?v=${videoId}`, `https://youtu.be/${videoId.replace("M", "%4D")}`,
    `https://youtube.com/watch?v=${videoId.replace("M", "%4D")}`,
    "https://example.test/a\nb.mp3", "https://example.test/a\\b.mp3", "https://example.test/" + "a".repeat(500),
  ]) assert.equal(invitationMusicSource(url).kind, "invalid", url);
});
test("MP3 client preflight checks extension/MIME/size; server must still validate bytes", () => {
  assert.equal(invitationAudioFileError({ name: "song.MP3", type: "audio/mpeg", size: 1234 }), null);
  assert.equal(invitationAudioFileError({ name: "song.mp3", type: "", size: 12 * 1024 * 1024 }), null);
  for (const file of [
    { name: "song.wav", type: "audio/wav", size: 1234 }, { name: "fake.mp3", type: "text/html", size: 1234 },
    { name: "empty.mp3", type: "audio/mpeg", size: 0 }, { name: "huge.mp3", type: "audio/mpeg", size: 12 * 1024 * 1024 + 1 },
  ]) assert.ok(invitationAudioFileError(file));
});

async function transpile(file, aliases = {}) {
  const source = await readFile(new URL(file, import.meta.url), "utf8");
  let js = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX } }).outputText;
  for (const dependency of ["react", "react/jsx-runtime"]) js = js.replaceAll(`from "${dependency}"`, `from "${import.meta.resolve(dependency)}"`);
  for (const [key, value] of Object.entries(aliases)) js = js.replaceAll(`from "${key}"`, `from "${value}"`);
  return `data:text/javascript;base64,${Buffer.from(js).toString("base64")}`;
}
const { InvitationMusicPlayer } = await import(await transpile("../src/features/journey/invitations/InvitationMusicPlayer.tsx", {
  "./invitationMusic": new URL("../src/features/journey/invitations/invitationMusic.ts", import.meta.url).href,
}));
const render = props => renderToStaticMarkup(createElement(InvitationMusicPlayer, props));
test("MP3 renders native playback without autoplay and invalid source leaves invitation safe", () => {
  const html = render({ value: "https://media.example.test/song.mp3" });
  assert.match(html, /<audio[^>]*controls=""[^>]*preload="none"/);
  assert.match(html, /src="https:\/\/media.example.test\/song.mp3"/);
  assert.doesNotMatch(html, /autoplay|<iframe/);
  assert.equal(render({ value: "" }), "");
  const invalid = render({ value: "javascript:alert(1)" });
  assert.doesNotMatch(invalid, /<audio|<iframe|javascript:/);
  assert.match(invalid, /RSVP/);
});
test("YouTube is opt-in, no external iframe before clicking, with safe fallback", () => {
  const html = render({ value: `https://youtu.be/${videoId}` });
  assert.match(html, /Mở trình phát YouTube/);
  assert.doesNotMatch(html, /<iframe|<audio/);
  assert.match(html, new RegExp(`href="https://www.youtube.com/watch\\?v=${videoId}"`));
  assert.match(html, /noopener noreferrer/);
  assert.match(render({ value: `https://youtu.be/${videoId}`, english: true }), /Load the YouTube player/);
});
test("builder wires music upload/save guard and public view uses same player", async () => {
  const builder = await readFile(new URL("../src/features/customer/components/invitation/InvitationBuilder.tsx", import.meta.url), "utf8");
  const picker = await readFile(new URL("../src/features/journey/invitations/InvitationMusicPicker.tsx", import.meta.url), "utf8");
  const player = await readFile(new URL("../src/features/journey/invitations/InvitationMusicPlayer.tsx", import.meta.url), "utf8");
  assert.match(builder, /coverUploading \|\| albumUploading \|\| musicUploading/);
  assert.match(builder, /disabled=\{saving \|\| uploading \|\| invalidMusic\}/);
  assert.match(picker, /\/api\/upload\/invitation-audio/);
  assert.match(picker, /abort\.current\?\.abort\(\)/);
  assert.doesNotMatch(picker, /axiosClient\.delete/);
  assert.match(player, /youtube-nocookie\.com\/embed\/\$\{source\.videoId\}\?autoplay=0&playsinline=1/);
  assert.match(player, /referrerPolicy="strict-origin-when-cross-origin"/);
});
