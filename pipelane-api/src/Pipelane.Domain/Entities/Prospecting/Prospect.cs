using System.Collections.Generic;

using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Domain.Entities.Prospecting;

public class Prospect : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Company { get; set; }
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public ProspectStatus Status { get; set; } = ProspectStatus.New;
    public bool OptedOut { get; set; }
    public DateTime? OptedOutAtUtc { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? SequenceId { get; set; }
    public Guid? CampaignId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastContactedAtUtc { get; set; }
    public DateTime? LastRepliedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? Persona { get; set; }
    public string? Industry { get; set; }
    public string? Region { get; set; }
    public string? Source { get; set; }
    public string? TagsJson { get; set; }
    public string? EnrichedJson { get; set; }
    public string? CustomFieldsJson { get; set; }

    public ProspectingSequence? Sequence { get; set; }
    public ProspectingCampaign? Campaign { get; set; }
    public ICollection<SendLog> Sends { get; set; } = new List<SendLog>();
    public ICollection<ProspectReply> Replies { get; set; } = new List<ProspectReply>();
    public ICollection<EmailGeneration> Generations { get; set; } = new List<EmailGeneration>();
}
