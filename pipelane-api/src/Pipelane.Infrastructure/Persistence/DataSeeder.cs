using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;
using Pipelane.Infrastructure.Security;

namespace Pipelane.Infrastructure.Persistence;

public class DataSeeder
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    public DataSeeder(IAppDbContext db, IPasswordHasher hasher) { _db = db; _hasher = hasher; }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Ensure a demo tenant and user exist
        var demoEmail = "demo@pipelane.local";
        var demoUser = _db.Users.FirstOrDefault(u => u.Email == demoEmail);
        Guid tenantId;
        if (demoUser is null)
        {
            tenantId = Guid.NewGuid();
            _db.Tenants.Add(new Tenant { Id = tenantId, Name = "Demo" });
            demoUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = demoEmail,
                PasswordHash = _hasher.Hash("Demo123!"),
                Role = "owner",
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(demoUser);
        }
        else
        {
            tenantId = demoUser.TenantId;
        }

        // Seed sample contacts/messages/templates if none for the tenant
        var hasContacts = _db.Contacts.Any(c => c.TenantId == tenantId);
        if (!hasContacts)
        {
            var random = new Random(42);
            var now = DateTime.UtcNow;
            var channels = new[] { Channel.Email, Channel.Whatsapp, Channel.Sms };

            for (var i = 0; i < 20; i++)
            {
                var language = i % 3 == 0 ? "fr" : "en";
                var createdAt = now.AddDays(-random.Next(1, 20));
                var contact = new Contact
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Phone = $"+1555123{i:0000}",
                    Email = $"contact{i}@demo.co",
                    FirstName = i % 2 == 0 ? "Alex" : "Charlie",
                    LastName = $"Demo{i}",
                    Lang = language,
                    TagsJson = "[\"segment:demo\",\"tz:" + (i % 2 == 0 ? "Europe/Paris" : "America/New_York") + "\"]",
                    CreatedAt = createdAt,
                    UpdatedAt = now
                };
                _db.Contacts.Add(contact);

                var conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = contact.Id,
                    PrimaryChannel = channels[i % channels.Length],
                    CreatedAt = createdAt.AddDays(1)
                };
                _db.Conversations.Add(conversation);

                if (i < 3)
                {
                    var outbound = new Message
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Channel = conversation.PrimaryChannel,
                        Direction = MessageDirection.Out,
                        Type = MessageType.Text,
                        PayloadJson = "{\"text\":\"Hi there, just sharing a quick update from Pipelane.\"}",
                        Status = MessageStatus.Sent,
                        CreatedAt = now.AddHours(-4 - i)
                    };
                    _db.Messages.Add(outbound);
                    _db.MessageEvents.Add(new MessageEvent
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        MessageId = outbound.Id,
                        Type = MessageEventType.Sent,
                        Provider = "seed",
                        CreatedAt = outbound.CreatedAt
                    });

                    var inbound = new Message
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Channel = conversation.PrimaryChannel,
                        Direction = MessageDirection.In,
                        Type = MessageType.Text,
                        PayloadJson = "{\"text\":\"Thanks, can we chat next week?\"}",
                        Status = MessageStatus.Delivered,
                        CreatedAt = outbound.CreatedAt.AddMinutes(30)
                    };
                    _db.Messages.Add(inbound);
                    _db.MessageEvents.Add(new MessageEvent
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        MessageId = inbound.Id,
                        Type = MessageEventType.Delivered,
                        Provider = "seed",
                        CreatedAt = inbound.CreatedAt
                    });
                }
                else
                {
                    var inbound = new Message
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Channel = conversation.PrimaryChannel,
                        Direction = MessageDirection.In,
                        Type = MessageType.Text,
                        PayloadJson = "{\"text\":\"Hello team\"}",
                        Status = MessageStatus.Delivered,
                        CreatedAt = now.AddHours(-random.Next(2, 36))
                    };
                    _db.Messages.Add(inbound);
                    _db.MessageEvents.Add(new MessageEvent
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        MessageId = inbound.Id,
                        Type = MessageEventType.Delivered,
                        Provider = "seed",
                        CreatedAt = inbound.CreatedAt
                    });
                }
            }
        }

        var hasTemplate = _db.Templates.Any(t => t.TenantId == tenantId && t.Name == "welcome" && t.Channel == Channel.Whatsapp && t.Lang == "en");
        if (!hasTemplate)
        {
            _db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "welcome",
                Channel = Channel.Whatsapp,
                Lang = "en",
                CoreSchemaJson = "{\"vars\":[\"name\"]}",
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        var hasProspecting = _db.Prospects.Any(p => p.TenantId == tenantId);
        if (!hasProspecting)
        {
            var now = DateTime.UtcNow;
            var sequenceId = Guid.NewGuid();
            var sequence = new ProspectingSequence
            {
                Id = sequenceId,
                TenantId = tenantId,
                Name = "Default Outreach",
                Description = "2-step prospecting sequence (J0 / J+3)",
                IsActive = true,
                TargetPersona = "RevOps Leaders",
                EntryCriteriaJson = "{\"industry\":\"SaaS\"}",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Steps = new List<ProspectingSequenceStep>
                {
                    new ProspectingSequenceStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        SequenceId = sequenceId,
                        Order = 0,
                        StepType = SequenceStepType.Email,
                        Channel = Channel.Email,
                        OffsetDays = 0,
                        PromptTemplate = "Opening email focusing on pipeline automation benefits.",
                        SubjectTemplate = "Quick idea for {{company}}",
                        RequiresApproval = false,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new ProspectingSequenceStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        SequenceId = sequenceId,
                        Order = 1,
                        StepType = SequenceStepType.Email,
                        Channel = Channel.Email,
                        OffsetDays = 3,
                        PromptTemplate = "Follow-up referencing previous note and case study.",
                        SubjectTemplate = "Thought this might help {{company}}",
                        RequiresApproval = false,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                }
            };
            _db.ProspectingSequences.Add(sequence);

            var campaign = new ProspectingCampaign
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SequenceId = sequence.Id,
                Name = "Warm SaaS leads",
                Status = ProspectingCampaignStatus.Draft,
                SegmentJson = "{\"source\":\"demo\"}",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ScheduledAtUtc = now.AddDays(1)
            };
            _db.ProspectingCampaigns.Add(campaign);

            var prospects = new List<Prospect>();
            var rng = new Random(84);
            for (int i = 0; i < 50; i++)
            {
                prospects.Add(new Prospect
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Email = $"prospect{i}@acme{i % 10}.com",
                    FirstName = $"Alex{i}",
                    LastName = "Demo",
                    Company = $"Acme {i % 10}",
                    Title = i % 3 == 0 ? "VP Sales" : "Head of Revenue",
                    Persona = "RevOps",
                    Source = "seed",
                    Status = ProspectStatus.New,
                    CreatedAtUtc = now.AddDays(-rng.Next(1, 20)),
                    UpdatedAtUtc = now.AddDays(-rng.Next(0, 5)),
                    TagsJson = "[\"demo\",\"import\"]"
                });
            }
            _db.Prospects.AddRange(prospects);
        }

        await _db.SaveChangesAsync(ct);

        await SeedAdditionalArtifactsAsync(tenantId, ct);

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedAdditionalArtifactsAsync(Guid tenantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var random = new Random(HashCode.Combine(tenantId, now.DayOfYear));

        var contacts = await _db.Contacts.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.CreatedAt)
            .Take(25)
            .ToListAsync(ct);

        if (contacts.Count == 0)
        {
            return;
        }

        var contactIds = contacts.Select(c => c.Id).ToList();

        var conversations = await _db.Conversations.AsNoTracking()
            .Where(c => c.TenantId == tenantId && contactIds.Contains(c.ContactId))
            .ToListAsync(ct);
        var conversationByContact = conversations
            .GroupBy(c => c.ContactId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAt).First());

        var conversationIds = conversations.Select(c => c.Id).ToList();

        var messages = await _db.Messages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && conversationIds.Contains(m.ConversationId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        var firstInbound = messages.FirstOrDefault(m => m.Direction == MessageDirection.In);
        var firstOutbound = messages.FirstOrDefault(m => m.Direction == MessageDirection.Out);

        var template = await _db.Templates.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var prospects = await _db.Prospects.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.CreatedAtUtc)
            .Take(60)
            .ToListAsync(ct);
        var prospectIds = prospects.Select(p => p.Id).ToList();

        var sequenceSteps = await _db.ProspectingSequenceSteps.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Order)
            .ToListAsync(ct);
        var firstStep = sequenceSteps.FirstOrDefault();

        var campaign = await _db.ProspectingCampaigns.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (!await _db.ChannelSettings.AnyAsync(c => c.TenantId == tenantId, ct))
        {
            _db.ChannelSettings.AddRange(new[]
            {
                new ChannelSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Channel = Channel.Email,
                    SettingsJson = "{\"from\":\"hello@pipelane.app\",\"provider\":\"resend\"}"
                },
                new ChannelSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Channel = Channel.Whatsapp,
                    SettingsJson = "{\"businessNumber\":\"+33755551212\",\"provider\":\"meta\"}"
                },
                new ChannelSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Channel = Channel.Sms,
                    SettingsJson = "{\"senderId\":\"PIPELANE\",\"provider\":\"twilio\"}"
                }
            });
        }

        if (!await _db.Consents.AnyAsync(c => c.TenantId == tenantId, ct))
        {
            var consents = new List<Consent>();
            foreach (var contact in contacts.Take(12))
            {
                consents.Add(new Consent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = contact.Id,
                    Channel = Channel.Email,
                    OptInAtUtc = now.AddDays(-random.Next(5, 45)),
                    Source = "demo_form",
                    MetaJson = "{\"ip\":\"127.0.0.1\"}"
                });
                consents.Add(new Consent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = contact.Id,
                    Channel = Channel.Whatsapp,
                    OptInAtUtc = now.AddDays(-random.Next(3, 30)),
                    Source = "demo_whatsapp",
                    MetaJson = "{\"keyword\":\"START\"}"
                });
            }

            _db.Consents.AddRange(consents);
        }

        if (!await _db.Campaigns.AnyAsync(c => c.TenantId == tenantId, ct) && template is not null)
        {
            _db.Campaigns.Add(new Campaign
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Relance WhatsApp J+2",
                PrimaryChannel = Channel.Whatsapp,
                TemplateId = template.Id,
                SegmentJson = "{\"tags\":[\"segment:demo\"]}",
                ScheduledAtUtc = now.AddHours(8),
                Status = CampaignStatus.Pending,
                CreatedAt = now.AddDays(-2)
            });
        }

        if (!await _db.Events.AnyAsync(e => e.TenantId == tenantId, ct))
        {
            _db.Events.AddRange(new[]
            {
                new Event
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Source = EventSource.Email,
                    PayloadJson = "{\"event\":\"opened\",\"campaign\":\"warm_saas\"}",
                    CreatedAt = now.AddHours(-6)
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Source = EventSource.Whatsapp,
                    PayloadJson = $"{{\"event\":\"reply\",\"contact\":\"{contacts[0].Email ?? "unknown"}\"}}",
                    CreatedAt = now.AddHours(-2)
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Source = EventSource.Crm,
                    PayloadJson = "{\"event\":\"deal_created\",\"amount\":1800}",
                    CreatedAt = now.AddHours(-1)
                }
            });
        }

        if (!await _db.Conversions.AnyAsync(c => c.TenantId == tenantId, ct) && contacts.FirstOrDefault() is { } conversionContact)
        {
            _db.Conversions.Add(new Conversion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ContactId = conversionContact.Id,
                CampaignId = template is null ? null : await _db.Campaigns
                    .Where(c => c.TenantId == tenantId)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync(ct),
                Amount = 1890m,
                Currency = "EUR",
                OrderId = $"DEMO-{random.Next(1000, 9999)}",
                RevenueAtUtc = now.AddHours(-3)
            });
        }

        if (!await _db.LeadScores.AnyAsync(l => l.TenantId == tenantId, ct))
        {
            var leadScores = new List<LeadScore>();
            if (contacts.FirstOrDefault() is { } leadContact)
            {
                leadScores.Add(new LeadScore
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = leadContact.Id,
                    Scope = "contact",
                    Score = 82,
                    ReasonsJson = "[\"Réponse rapide\",\"Ouverture campagne J+1\"]",
                    Model = "contact_v1",
                    UpdatedAtUtc = now.AddHours(-4)
                });
            }

            if (prospects.FirstOrDefault() is { } leadProspect)
            {
                leadScores.Add(new LeadScore
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProspectId = leadProspect.Id,
                    Scope = "prospect",
                    Score = 91,
                    ReasonsJson = "[\"Score Hunter élevé\",\"Site web rapide\"]",
                    Model = "hunter_v1",
                    UpdatedAtUtc = now.AddHours(-2)
                });
            }

            if (leadScores.Count > 0)
            {
                _db.LeadScores.AddRange(leadScores);
            }
        }

        if (!await _db.FollowupTasks.AnyAsync(t => t.TenantId == tenantId, ct) && contacts.FirstOrDefault() is { } followContact)
        {
            var relatedConversation = conversationByContact.TryGetValue(followContact.Id, out var convo)
                ? convo
                : conversations.FirstOrDefault();

            _db.FollowupTasks.AddRange(new[]
            {
                new FollowupTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = followContact.Id,
                    MessageId = firstInbound?.Id,
                    Title = "Relancer pour créneau de démo",
                    DueAtUtc = now.AddHours(6),
                    CreatedAtUtc = now.AddHours(-1),
                    Completed = false,
                    Notes = relatedConversation is null ? null : $"Conversation {relatedConversation.Id.ToString()[..8]}"
                },
                new FollowupTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = contacts.ElementAtOrDefault(1)?.Id ?? followContact.Id,
                    MessageId = firstOutbound?.Id,
                    Title = "Envoyer étude de cas",
                    DueAtUtc = now.AddDays(1),
                    CreatedAtUtc = now,
                    Completed = true,
                    CompletedAtUtc = now.AddHours(1),
                    Notes = "Document partagé via email."
                }
            });
        }

        if (!await _db.Outbox.AnyAsync(o => o.TenantId == tenantId, ct) && contacts.FirstOrDefault() is { } outboxContact)
        {
            var relatedConversation = conversationByContact.TryGetValue(outboxContact.Id, out var convo)
                ? convo
                : conversations.FirstOrDefault();

            _db.Outbox.AddRange(new[]
            {
                new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = outboxContact.Id,
                    ConversationId = relatedConversation?.Id,
                    Channel = Channel.Email,
                    Type = MessageType.Text,
                    TemplateId = template?.Id,
                    PayloadJson = "{\"subject\":\"On boucle votre démo\",\"body\":\"Bonjour, voici le lien pour réserver.\"}",
                    MetaJson = "{\"priority\":\"high\"}",
                    ScheduledAtUtc = now.AddMinutes(30),
                    Attempts = 0,
                    MaxAttempts = 5,
                    Status = OutboxStatus.Queued,
                    CreatedAt = now
                },
                new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = contacts.ElementAtOrDefault(2)?.Id ?? outboxContact.Id,
                    ConversationId = relatedConversation?.Id,
                    Channel = Channel.Whatsapp,
                    Type = MessageType.Template,
                    TemplateId = template?.Id,
                    PayloadJson = "{\"template\":\"welcome\",\"lang\":\"fr\"}",
                    MetaJson = "{\"attempt\":1}",
                    ScheduledAtUtc = now.AddMinutes(-15),
                    Attempts = 1,
                    MaxAttempts = 5,
                    Status = OutboxStatus.Sending,
                    CreatedAt = now.AddMinutes(-20),
                    LockedUntilUtc = now.AddMinutes(5)
                },
                new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = contacts.ElementAtOrDefault(3)?.Id ?? outboxContact.Id,
                    ConversationId = relatedConversation?.Id,
                    Channel = Channel.Sms,
                    Type = MessageType.Text,
                    PayloadJson = "{\"text\":\"Rappel: réservez votre créneau\"}",
                    MetaJson = "{\"quietHours\":true}",
                    ScheduledAtUtc = now.AddHours(-5),
                    Attempts = 2,
                    MaxAttempts = 3,
                    Status = OutboxStatus.Failed,
                    LastError = "Quiet hours in effect",
                    CreatedAt = now.AddHours(-6)
                }
            });
        }

        if (!await _db.RateLimitSnapshots.AnyAsync(r => r.TenantId == tenantId, ct))
        {
            _db.RateLimitSnapshots.Add(new RateLimitSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TargetTenantId = tenantId,
                Scope = "whatsapp:messages",
                HitsJson = "[{\"window\":\"2025-01-01T10:00:00Z\",\"count\":12},{\"window\":\"2025-01-01T11:00:00Z\",\"count\":8}]",
                WindowStartUtc = now.AddHours(-1),
                UpdatedAtUtc = now
            });
        }

        if (!await _db.FailedWebhooks.AnyAsync(f => f.TenantId == tenantId, ct))
        {
            _db.FailedWebhooks.Add(new FailedWebhook
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Channel = Channel.Email,
                Provider = "resend",
                Kind = "delivery",
                Payload = "{\"event\":\"bounce\",\"messageId\":\"demo\"}",
                HeadersJson = "{\"signature\":\"abc123\"}",
                LastError = "HTTP 429 Too Many Requests",
                RetryCount = 3,
                NextAttemptUtc = now.AddMinutes(10),
                CreatedAtUtc = now.AddMinutes(-30),
                UpdatedAtUtc = now.AddMinutes(-5)
            });
        }

        if (!await _db.ProspectLists.AnyAsync(l => l.TenantId == tenantId, ct) && prospects.Count >= 5)
        {
            var listId = Guid.NewGuid();
            var list = new ProspectList
            {
                Id = listId,
                TenantId = tenantId,
                Name = "Top 20 Hunter",
                CreatedAtUtc = now.AddDays(-1),
                UpdatedAtUtc = now
            };

            _db.ProspectLists.Add(list);

            var items = prospects.Take(20).Select((prospect, index) => new ProspectListItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProspectListId = listId,
                ProspectId = prospect.Id,
                AddedAtUtc = now.AddMinutes(-index * 3)
            }).ToList();

            _db.ProspectListItems.AddRange(items);
        }

        if (!await _db.EmailGenerations.AnyAsync(g => g.TenantId == tenantId, ct) && prospects.FirstOrDefault() is { } targetProspect && firstStep is not null)
        {
            var generationId = Guid.NewGuid();
            var generation = new EmailGeneration
            {
                Id = generationId,
                TenantId = tenantId,
                ProspectId = targetProspect.Id,
                StepId = firstStep.Id,
                CampaignId = campaign?.Id,
                Variant = "A",
                Subject = $"Automatisation pipeline - {targetProspect.Company}",
                HtmlBody = "<p>Bonjour {{firstName}},</p><p>On automatise votre prospection.</p>",
                TextBody = "Bonjour, on automatise votre prospection.",
                PromptUsed = "Write a friendly outreach message",
                Model = "gpt-4o-mini",
                Temperature = 0.3m,
                PromptTokens = 420,
                CompletionTokens = 181,
                CostUsd = 0.08m,
                Approved = true,
                CreatedAtUtc = now.AddHours(-2),
                MetadataJson = "{\"persona\":\"RevOps\"}"
            };

            _db.EmailGenerations.Add(generation);

            if (!await _db.ProspectingSendLogs.AnyAsync(s => s.TenantId == tenantId, ct))
            {
                var sendLogId = Guid.NewGuid();
                var sendLog = new SendLog
                {
                    Id = sendLogId,
                    TenantId = tenantId,
                    ProspectId = targetProspect.Id,
                    CampaignId = campaign?.Id,
                    StepId = firstStep.Id,
                    GenerationId = generationId,
                    Provider = "resend",
                    ProviderMessageId = $"msg_{random.Next(100000, 999999)}",
                    Status = SendLogStatus.Delivered,
                    ScheduledAtUtc = now.AddHours(-1),
                    SentAtUtc = now.AddMinutes(-50),
                    DeliveredAtUtc = now.AddMinutes(-47),
                    OpenedAtUtc = now.AddMinutes(-30),
                    ClickedAtUtc = now.AddMinutes(-10),
                    RetryCount = 0,
                    RawPayloadJson = "{\"status\":\"delivered\"}",
                    MetadataJson = "{\"utm\":\"demo\"}",
                    CreatedAtUtc = now.AddHours(-1),
                    UpdatedAtUtc = now.AddMinutes(-5)
                };

                _db.ProspectingSendLogs.Add(sendLog);

                if (!await _db.ProspectReplies.AnyAsync(r => r.TenantId == tenantId, ct))
                {
                    _db.ProspectReplies.Add(new ProspectReply
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ProspectId = targetProspect.Id,
                        CampaignId = campaign?.Id,
                        SendLogId = sendLogId,
                        StepId = firstStep.Id,
                        Provider = "imap",
                        ProviderMessageId = $"reply_{random.Next(100000, 999999)}",
                        ReceivedAtUtc = now.AddMinutes(-15),
                        Subject = "Re: Automatisation pipeline",
                        TextBody = "Intéressant, pouvez-vous partager un créneau ?",
                        HtmlBody = "<p>Intéressant, pouvez-vous partager un créneau ?</p>",
                        Intent = ReplyIntent.Interested,
                        IntentConfidence = 0.82,
                        ExtractedDatesJson = "[\"2025-11-02T10:00:00Z\"]",
                        AutoReplySuggested = true,
                        AutoReplyGenerationId = generationId,
                        CreatedAtUtc = now.AddMinutes(-15),
                        ProcessedAtUtc = now.AddMinutes(-5),
                        MetadataJson = "{\"timezone\":\"Europe/Paris\"}"
                    });
                }
            }
        }
    }
}
