export interface ChatMessage {
  id: string;
  leadId: string;
  senderId: string;
  senderName: string;
  isFromCustomer: boolean;
  content: string;
  isQuote: boolean;
  quoteAmount?: number;
  quoteDescription?: string;
  isRead: boolean;
  createdAt: string;
}

export interface SendMessagePayload {
  content: string;
  isQuote?: boolean;
  quoteAmount?: number;
  quoteDescription?: string;
}
