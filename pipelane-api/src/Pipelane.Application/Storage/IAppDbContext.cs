using Microsoft.EntityFrameworkCore;

using Pipelane.Domain.Entities;
using Pipelane.Domain.Entities.Prospecting;

namespace Pipelane.Application.Storage;

public interface IAppDbContext
{
    DbSet<Contact> Contacts { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<Consent> Consents { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageEvent> MessageEvents { get; }
    DbSet<Template> Templates { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<Event> Events { get; }
    DbSet<Conversion> Conversions { get; }
    DbSet<LeadScore> LeadScores { get; }
    DbSet<ChannelSettings> ChannelSettings { get; }
    DbSet<OutboxMessage> Outbox { get; }
    DbSet<User> Users { get; }
    DbSet<FollowupTask> FollowupTasks { get; }
    DbSet<Prospect> Prospects { get; }
    DbSet<ProspectingSequence> ProspectingSequences { get; }
    DbSet<ProspectingSequenceStep> ProspectingSequenceSteps { get; }
    DbSet<ProspectingCampaign> ProspectingCampaigns { get; }
    DbSet<EmailGeneration> EmailGenerations { get; }
    DbSet<SendLog> ProspectingSendLogs { get; }
    DbSet<ProspectReply> ProspectReplies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
