using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
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
            var r = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                var c = new Contact
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Phone = $"+1555000{i:D3}",
                    Email = $"user{i}@example.com",
                    FirstName = "User",
                    LastName = i.ToString(),
                    Lang = i % 2 == 0 ? "en" : "fr",
                    CreatedAt = DateTime.UtcNow.AddDays(-r.Next(1, 10)),
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Contacts.Add(c);
                var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = c.Id, PrimaryChannel = Channel.Whatsapp, CreatedAt = DateTime.UtcNow.AddDays(-1) };
                _db.Conversations.Add(convo);
                _db.Messages.Add(new Message { Id = Guid.NewGuid(), TenantId = tenantId, ConversationId = convo.Id, Channel = Channel.Whatsapp, Direction = MessageDirection.In, Type = MessageType.Text, PayloadJson = "{\"text\":\"Hello\"}", Status = MessageStatus.Delivered, CreatedAt = DateTime.UtcNow.AddHours(-r.Next(1, 48)) });
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

        await _db.SaveChangesAsync(ct);
    }
}
