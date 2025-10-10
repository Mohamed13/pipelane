export type Channel = 'whatsapp' | 'email' | 'sms';

export interface Contact {
  id: string;
  firstName?: string | null;
  lastName?: string | null;
  phone?: string | null;
  email?: string | null;
  lang?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PagedContactsResponse {
  total: number;
  items: Contact[];
}

export type MessageDirection = 'in' | 'out';
export type MessageType = 'text' | 'template' | 'media';
export type MessageStatus = 'queued' | 'sent' | 'delivered' | 'opened' | 'failed' | 'bounced';

export interface Message {
  id: string;
  conversationId: string;
  channel: Channel;
  direction: MessageDirection;
  type: MessageType;
  templateName?: string | null;
  lang?: string | null;
  payloadJson?: string | null;
  status: MessageStatus;
  provider?: string | null;
  providerMessageId?: string | null;
  errorCode?: string | null;
  errorReason?: string | null;
  deliveredAt?: string | null;
  openedAt?: string | null;
  failedAt?: string | null;
  createdAt: string;
}

export interface ConversationResponse {
  conversationId?: string;
  messages: Message[];
}

export interface TemplateSummary {
  id: string;
  tenantId: string;
  name: string;
  channel: Channel;
  lang: string;
  category?: string | null;
  coreSchemaJson: string;
  isActive: boolean;
  updatedAtUtc: string;
}

export interface DeliveryTotals {
  queued: number;
  sent: number;
  delivered: number;
  opened: number;
  failed: number;
  bounced: number;
}

export interface DeliveryChannelBreakdown extends DeliveryTotals {
  channel: string;
}

export interface DeliveryTemplateBreakdown extends DeliveryTotals {
  template: string;
  channel: string;
}

export interface DeliveryAnalyticsResponse {
  totals: DeliveryTotals;
  byChannel: DeliveryChannelBreakdown[];
  byTemplate: DeliveryTemplateBreakdown[];
}

export interface SendMessageRequestPayload {
  contactId?: string;
  phone?: string;
  channel: Channel;
  type: MessageType;
  text?: string;
  templateName?: string;
  lang?: string;
  variables?: Record<string, string> | null;
  meta?: Record<string, string> | null;
}

export interface CampaignCreatePayload {
  name: string;
  primaryChannel: Channel;
  fallbackOrderJson?: string | null;
  templateId: string;
  segmentJson: string;
  scheduledAtUtc?: string | null;
  batchSize?: number | null;
}

export interface CampaignDetail extends CampaignCreatePayload {
  id: string;
  tenantId: string;
  status: string;
  createdAt: string;
}

export interface ChannelSettingsPayload {
  channel: Channel;
  settings: Record<string, string>;
}

export interface FollowupPreviewResponse {
  count: number;
}

export interface WhatsAppSettings {
  phone_number_id?: string;
  access_token?: string;
  verify_token?: string;
}

export interface EmailSettings {
  apiKey?: string;
  domain?: string;
}

export interface SmsSettings {
  apiKey?: string;
}

export const ChannelLabels: Record<Channel, string> = {
  whatsapp: 'WhatsApp',
  email: 'Email',
  sms: 'SMS',
};
