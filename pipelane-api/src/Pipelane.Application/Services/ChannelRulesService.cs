using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Application.Services;

public interface IChannelRulesService
{
    Task<bool> CanSendWhatsAppSessionAsync(Guid contactId, CancellationToken ct);
    bool MustIncludeUnsubscribe(Channel channel);
}

public sealed class ChannelRulesService : IChannelRulesService
{
    private readonly IAppDbContext _db;

    public ChannelRulesService(IAppDbContext db) => _db = db;

    public async Task<bool> CanSendWhatsAppSessionAsync(Guid contactId, CancellationToken ct)
    {
        var list = await (from m in _db.Messages
                          join c in _db.Conversations on m.ConversationId equals c.Id
                          where c.ContactId == contactId
                                && m.Direction == MessageDirection.In
                                && m.Channel == Channel.Whatsapp
                          orderby m.CreatedAt descending
                          select (DateTime?)m.CreatedAt)
                          .ToListAsync(ct);
        var lastInbound = list.FirstOrDefault();
        return lastInbound.HasValue && (DateTime.UtcNow - lastInbound.Value).TotalHours <= 24;
    }

    public bool MustIncludeUnsubscribe(Channel channel) => channel == Channel.Email;
}
