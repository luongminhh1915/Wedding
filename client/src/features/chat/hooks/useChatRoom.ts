import { useState, useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { ChatMessage, SendMessagePayload } from '../../../types/chat.types';

export function useChatRoom(leadId: string) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // 1. Tải lịch sử tin nhắn từ REST API
  const fetchHistory = useCallback(async () => {
    try {
      setIsLoading(true);
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/leads/${leadId}/messages`, {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        throw new Error('Không thể tải lịch sử trò chuyện.');
      }

      const data: ChatMessage[] = await res.json();
      setMessages(data);
    } catch (err: any) {
      setError(err.message || 'Lỗi tải tin nhắn.');
    } finally {
      setIsLoading(false);
    }
  }, [leadId]);

  // 2. Khởi tạo kết nối SignalR realtime
  useEffect(() => {
    if (!leadId) return;

    fetchHistory();

    const token = localStorage.getItem('token') || '';

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/chat`, {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // Lắng nghe tin nhắn realtime từ Hub
    connection.on('ReceiveMessage', (newMessage: ChatMessage) => {
      setMessages((prev) => {
        // Tránh trùng lặp id
        if (prev.some((m) => m.id === newMessage.id)) return prev;
        return [...prev, newMessage];
      });
    });

    connection
      .start()
      .then(async () => {
        setIsConnected(true);
        // Tham gia phòng chat của lead
        await connection.invoke('JoinLeadRoom', leadId);
      })
      .catch((err) => {
        console.warn('SignalR connection failed, falling back to REST API polling:', err);
        setIsConnected(false);
      });

    return () => {
      if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke('LeaveLeadRoom', leadId).catch(() => {});
      }
      connection.stop();
      connectionRef.current = null;
    };
  }, [leadId, fetchHistory]);

  // 3. Gửi tin nhắn hoặc báo giá
  const sendMessage = async (payload: SendMessagePayload) => {
    try {
      const conn = connectionRef.current;
      if (conn && conn.state === signalR.HubConnectionState.Connected) {
        // Gửi qua WebSocket SignalR
        await conn.invoke(
          'SendMessage',
          leadId,
          payload.content,
          payload.isQuote || false,
          payload.quoteAmount || null,
          payload.quoteDescription || null
        );
      } else {
        // Fallback qua REST API nếu SignalR ngắt
        const token = localStorage.getItem('token');
        const res = await fetch(`/api/leads/${leadId}/messages`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            ...(token ? { Authorization: `Bearer ${token}` } : {})
          },
          body: JSON.stringify(payload)
        });

        if (!res.ok) {
          throw new Error('Gửi tin nhắn thất bại.');
        }

        const sentMsg: ChatMessage = await res.json();
        setMessages((prev) => {
          if (prev.some((m) => m.id === sentMsg.id)) return prev;
          return [...prev, sentMsg];
        });
      }
    } catch (err: any) {
      throw new Error(err.message || 'Không thể gửi tin nhắn.');
    }
  };

  return {
    messages,
    isLoading,
    isConnected,
    error,
    sendMessage,
    refetch: fetchHistory
  };
}
