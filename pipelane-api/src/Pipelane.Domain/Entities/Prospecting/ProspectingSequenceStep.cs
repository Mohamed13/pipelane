using System.Collections.Generic;

using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Domain.Entities.Prospecting;

public class ProspectingSequenceStep : BaseEntity
{
    public Guid SequenceId { get; set; }
    public int Order { get; set; }
    public SequenceStepType StepType { get; set; } = SequenceStepType.Email;
    public Channel Channel { get; set; } = Channel.Email;
    public int OffsetDays { get; set; }
    public TimeSpan? SendWindowStartUtc { get; set; }
    public TimeSpan? SendWindowEndUtc { get; set; }
    public string? PromptTemplate { get; set; }
    public string? SubjectTemplate { get; set; }
    public string? GuardrailInstructions { get; set; }
    public bool RequiresApproval { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ProspectingSequence? Sequence { get; set; }
    public ICollection<EmailGeneration> Generations { get; set; } = new List<EmailGeneration>();
    public ICollection<SendLog> Sends { get; set; } = new List<SendLog>();
    public ICollection<ProspectReply> Replies { get; set; } = new List<ProspectReply>();
}
