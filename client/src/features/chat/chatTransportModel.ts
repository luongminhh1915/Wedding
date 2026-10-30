import type { ChatMessage } from "../../types/chat.types";

export function chatHubUrl(apiBase: string) {
  return `${apiBase.trim().replace(/\/+$/, "")}/hubs/chat`;
}
/** History can finish after a realtime delivery. Keep both without showing a delivered message twice. */
export function mergeChatMessages(current: ChatMessage[], incoming: ChatMessage[]) {
  const messages = new Map(current.map(message => [message.id, message]));
  for (const message of incoming) messages.set(message.id, message);
  return [...messages.values()].sort((left, right) => {
    const difference = Date.parse(left.createdAt) - Date.parse(right.createdAt);
    return Number.isFinite(difference) && difference !== 0 ? difference : left.id.localeCompare(right.id);
  });
}
