import { test } from "node:test";
import assert from "node:assert/strict";
import { chatHubUrl, mergeChatMessages } from "../src/features/chat/chatTransportModel.ts";

test("chat hub follows the configured API origin and deployment path", () => {
  assert.equal(chatHubUrl(""), "/hubs/chat");
  assert.equal(chatHubUrl("https://api.example.invalid/"), "https://api.example.invalid/hubs/chat");
  assert.equal(chatHubUrl("https://api.example.invalid/wedding///"), "https://api.example.invalid/wedding/hubs/chat");
  assert.equal(chatHubUrl("/backend/"), "/backend/hubs/chat");
});
test("history racing realtime preserves new deliveries once and keeps chronological conversation order", () => {
  const first = { id: "first", createdAt: "2026-10-20T10:00:00Z", content: "Original" };
  const latest = { id: "latest", createdAt: "2026-10-20T10:00:02Z", content: "Realtime arrived first" };
  const middle = { id: "middle", createdAt: "2026-10-20T10:00:01Z", content: "Stored history" };
  const current = [latest];
  const result = mergeChatMessages(current, [middle, first, { ...latest }]);
  assert.deepEqual(result.map(message => message.id), ["first", "middle", "latest"]);
  assert.deepEqual(current, [latest]);
  assert.equal(mergeChatMessages(result, [latest]).length, 3);
});
