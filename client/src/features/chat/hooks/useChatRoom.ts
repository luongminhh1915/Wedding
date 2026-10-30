import { useState, useEffect, useRef, useCallback } from 'react';
import axios from 'axios';
import * as signalR from '@microsoft/signalr';
import axiosClient from '../../../services/axiosClient';
import { useAuthStore } from '../../../store/authStore';
import type { ChatMessage, SendMessagePayload } from '../../../types/chat.types';
import { chatHubUrl, mergeChatMessages } from '../chatTransportModel';

function messageError(error: unknown, fallback: string) {
  if (axios.isAxiosError<{ message?: string; detail?: string }>(error))
    return error.response?.data?.message ?? error.response?.data?.detail ?? fallback;
  return error instanceof Error ? error.message : fallback;
}

export function useChatRoom(leadId: string) {
  const token = useAuthStore(state => state.token);
  const userId = useAuthStore(state => state.user?.id);
  const revision = useAuthStore(state => state.sessionRevision);
  const roomKey = `${userId ?? ''}:${revision}:${leadId}`;
  const currentRoom = useRef(roomKey);
  currentRoom.current = roomKey;
  const [history, setHistory] = useState<{ roomKey: string; messages: ChatMessage[] }>({ roomKey, messages: [] });
  const [isLoading, setIsLoading] = useState(true);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const historyRequest = useRef<AbortController | null>(null);

  const appendMessages = useCallback((incoming: ChatMessage[]) => {
    if (currentRoom.current !== roomKey) return;
    setHistory(previous => ({ roomKey, messages: mergeChatMessages(previous.roomKey === roomKey ? previous.messages : [], incoming) }));
  }, [roomKey]);

  const fetchHistory = useCallback(async (quiet = false) => {
    if (!leadId || !token || !userId) { setIsLoading(false); return; }
    historyRequest.current?.abort();
    const request = new AbortController(); historyRequest.current = request;
    try {
      if (!quiet) setIsLoading(true);
      const response = await axiosClient.get<ChatMessage[]>(`/api/leads/${leadId}/messages`, { signal: request.signal });
      if (request.signal.aborted || currentRoom.current !== roomKey) return;
      appendMessages(response.data); setError(null);
    } catch (failure) {
      if (!request.signal.aborted && currentRoom.current === roomKey)
        setError(messageError(failure, 'Không thể tải lịch sử trò chuyện.'));
    } finally {
      if (!request.signal.aborted && currentRoom.current === roomKey) setIsLoading(false);
    }
  }, [leadId, token, userId, roomKey, appendMessages]);

  useEffect(() => {
    setHistory({ roomKey, messages: [] }); setError(null); setIsConnected(false);
    if (!leadId || !token || !userId) { setIsLoading(false); return; }
    let active = true;
    void fetchHistory();
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(chatHubUrl(import.meta.env.VITE_API_URL || ''), {
        accessTokenFactory: () => useAuthStore.getState().token ?? '',
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    connectionRef.current = connection;
    connection.on('ReceiveMessage', (message: ChatMessage) => { if (active) appendMessages([message]); });
    const join = async () => {
      await connection.invoke('JoinLeadRoom', leadId);
      if (active && currentRoom.current === roomKey) { setIsConnected(true); void fetchHistory(true); }
    };
    connection.onreconnecting(() => { if (active) setIsConnected(false); });
    connection.onreconnected(() => { void join().catch(() => { if (active) setIsConnected(false); }); });
    connection.onclose(() => { if (active) setIsConnected(false); });
    void connection.start().then(join).catch(() => { if (active) setIsConnected(false); });
    return () => {
      active = false; historyRequest.current?.abort();
      if (connectionRef.current === connection) connectionRef.current = null;
      void connection.stop().catch(() => {});
    };
  }, [leadId, token, userId, roomKey, fetchHistory, appendMessages]);

  // Poll only while realtime is unavailable. It uses the same authenticated client and room boundary.
  useEffect(() => {
    if (isConnected || !leadId || !token || !userId) return;
    const interval = window.setInterval(() => { void fetchHistory(true); }, 5000);
    return () => window.clearInterval(interval);
  }, [isConnected, leadId, token, userId, fetchHistory]);

  const sendMessage = async (payload: SendMessagePayload) => {
    if (!token || !userId || currentRoom.current !== roomKey) throw new Error('Vui lòng đăng nhập lại để gửi tin nhắn.');
    try {
      const connection = connectionRef.current;
      if (isConnected && connection?.state === signalR.HubConnectionState.Connected) {
        await connection.invoke('SendMessage', leadId, payload.content, payload.isQuote ?? false,
          payload.quoteAmount ?? null, payload.quoteDescription ?? null);
      } else {
        const response = await axiosClient.post<ChatMessage>(`/api/leads/${leadId}/messages`, payload);
        appendMessages([response.data]);
      }
    } catch (failure) { throw new Error(messageError(failure, 'Không thể gửi tin nhắn.')); }
  };

  return { messages: history.roomKey === roomKey ? history.messages : [], isLoading, isConnected, error, sendMessage, refetch: fetchHistory };
}
