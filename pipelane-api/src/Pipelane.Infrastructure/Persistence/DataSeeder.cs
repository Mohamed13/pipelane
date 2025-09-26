using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Persistence;

public class DataSeeder
{
    private readonly IAppDbContext _db;
    public DataSeeder(IAppDbContext db) => _db = db;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (_db.Contacts.Any()) return;

        var tenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = tenantId, Name = "Demo" });

        var r = new Random(42);
        for (int i = 0; i < 10; i++)
        {
            var c = new Contact
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Phone = $"+1555000{i:D3}", Email = $"user{i}@example.com",
                FirstName = "User", LastName = i.ToString(), Lang = i % 2 == 0 ? "en" : "fr",
                CreatedAt = DateTime.UtcNow.AddDays(-r.Next(1, 10)), UpdatedAt = DateTime.UtcNow
            };
            _db.Contacts.Add(c);
            var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = c.Id, PrimaryChannel = Channel.Whatsapp, CreatedAt = DateTime.UtcNow.AddDays(-1) };
            _db.Conversations.Add(convo);
            _db.Messages.Add(new Message { Id = Guid.NewGuid(), TenantId = tenantId, ConversationId = convo.Id, Channel = Channel.Whatsapp, Direction = MessageDirection.In, Type = MessageType.Text, PayloadJson = "{\"text\":\"Hello\"}", Status = MessageStatus.Delivered, CreatedAt = DateTime.UtcNow.AddHours(-r.Next(1, 48)) });
        }

        _db.Templates.Add(new Template
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "welcome", Channel = Channel.Whatsapp, Lang = "en",
            CoreSchemaJson = "{\"vars\":[\"name\"]}", IsActive = true, UpdatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}

