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

export interface SendMessageResponse {
  success: boolean;
  providerMessageId?: string | null;
  error?: string | null;
}

export interface CampaignCreatePayload {
  name: string;
  primaryChannel: Channel;
  fallbackOrderJson?: string | null;
  templateId: string;
  segmentJson: string;
  scheduledAtUtc?: string | null;
  batchSize?: number | null;
  smartFollowupDefault?: boolean;
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

export type ProspectStatus =
  | 'new'
  | 'enriching'
  | 'scheduled'
  | 'active'
  | 'replied'
  | 'meetingBooked'
  | 'optedOut';

export interface ProspectRecord {
  id: string;
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  company?: string | null;
  title?: string | null;
  status: ProspectStatus;
  optedOut: boolean;
  createdAt: string;
  updatedAt: string;
  lastContactedAt?: string | null;
  lastRepliedAt?: string | null;
  sequenceId?: string | null;
  campaignId?: string | null;
}

export interface ProspectImportResult {
  imported: number;
  skipped: number;
  updated: number;
}

export type SequenceStepType = 'email' | 'wait' | 'task';

export interface ProspectingSequenceStep {
  id: string;
  order: number;
  stepType: SequenceStepType;
  channel: Channel;
  offsetDays: number;
  promptTemplate?: string | null;
  subjectTemplate?: string | null;
  guardrailInstructions?: string | null;
  requiresApproval: boolean;
}

export interface ProspectingSequence {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  targetPersona?: string | null;
  entryCriteriaJson?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  steps: ProspectingSequenceStep[];
}

export interface ProspectingSequencePayload {
  name: string;
  description?: string | null;
  isActive: boolean;
  targetPersona?: string | null;
  entryCriteriaJson?: string | null;
  steps: Array<Omit<ProspectingSequenceStep, 'id'>>;
}

export type ProspectingCampaignStatus = 'draft' | 'scheduled' | 'running' | 'paused' | 'completed' | 'cancelled';

export interface ProspectingCampaign {
  id: string;
  name: string;
  sequenceId: string;
  status: ProspectingCampaignStatus;
  segmentJson: string;
  settingsJson?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  scheduledAtUtc?: string | null;
  startedAtUtc?: string | null;
  pausedAtUtc?: string | null;
  completedAtUtc?: string | null;
}

export interface ProspectingCampaignCreateRequest {
  name: string;
  sequenceId: string;
  segmentJson: string;
  settingsJson?: string | null;
  scheduledAtUtc?: string | null;
}

export interface ProspectingCampaignPreviewStep {
  stepId: string;
  scheduledAtUtc: string;
  label: string;
}

export interface ProspectingCampaignPreview {
  campaignId: string;
  prospects: number;
  steps: ProspectingCampaignPreviewStep[];
}

export interface ProspectingSeriesPoint {
  date: string;
  sent: number;
  opened: number;
  replies: number;
  booked: number;
}

export interface ProspectingStepBreakdown {
  stepId: string;
  label: string;
  sent: number;
  replies: number;
  meetings: number;
}

export interface ProspectingAnalyticsResponse {
  totalProspects: number;
  activeCampaigns: number;
  emailsSent: number;
  emailsOpened: number;
  repliesReceived: number;
  meetingsBooked: number;
  dailySeries: ProspectingSeriesPoint[];
  stepBreakdown: ProspectingStepBreakdown[];
}

export type ReplyIntent =
  | 'unknown'
  | 'interested'
  | 'meetingRequested'
  | 'notInterested'
  | 'followUp'
  | 'support'
  | 'outOfOffice'
  | 'bounce'
  | 'unsubscribe';

export interface ProspectReplyRecord {
  id: string;
  prospectId: string;
  campaignId?: string | null;
  sendLogId?: string | null;
  subject?: string | null;
  textBody?: string | null;
  htmlBody?: string | null;
  intent: ReplyIntent;
  intentConfidence?: number | null;
  receivedAtUtc: string;
  processedAtUtc?: string | null;
}

export interface GenerateProspectingEmailRequest {
  prospectId: string;
  stepId: string;
  campaignId?: string | null;
  variant?: string;
}

export interface GenerateProspectingEmailResponse {
  generationId: string;
  subject: string;
  htmlBody: string;
  textBody?: string | null;
  variant: string;
  promptTokens?: number | null;
  completionTokens?: number | null;
  costUsd?: number | null;
}

export interface ClassifyReplyResponse {
  replyId: string;
  intent: ReplyIntent;
  confidence: number;
  extractedDatesJson?: string | null;
}

export interface AutoReplyResponse {
  generationId: string;
  subject: string;
  htmlBody: string;
  textBody?: string | null;
  variant: string;
}

export interface AiMessageContext {
  firstName?: string | null;
  lastName?: string | null;
  company?: string | null;
  role?: string | null;
  painPoints?: string[] | null;
  pitch: string;
  calendlyUrl?: string | null;
  lastMessageSnippet?: string | null;
}

export interface AiGenerateMessageRequest {
  contactId?: string | null;
  language?: string | null;
  channel: Channel;
  context: AiMessageContext;
}

export interface AiGenerateMessageResponse {
  subject?: string | null;
  text: string;
  html?: string | null;
  languageDetected?: string | null;
}

export type AiIntent =
  | 'Interested'
  | 'Maybe'
  | 'NotNow'
  | 'NotRelevant'
  | 'OOO'
  | 'AutoReply';

export interface AiClassifyReplyRequest {
  text: string;
  language?: string | null;
}

export interface AiClassifyReplyResponse {
  intent: AiIntent;
  confidence: number;
}

export interface AiPerformanceHints {
  goodHours?: number[] | null;
  badDays?: string[] | null;
}

export interface AiSuggestFollowupRequest {
  channel: Channel;
  timezone: string;
  lastInteractionAt: string;
  read: boolean;
  language?: string | null;
  historySnippet?: string | null;
  performanceHints?: AiPerformanceHints | null;
}

export interface AiSuggestFollowupResponse {
  scheduledAtIso: string;
  angle: 'reminder' | 'value' | 'social' | 'question';
  previewText: string;
}
