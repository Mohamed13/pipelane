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
    }
}
