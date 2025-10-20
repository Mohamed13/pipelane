using System.Collections.Generic;

namespace Pipelane.Domain.Entities.Prospecting;

public class EmailGeneration : BaseEntity
{
    public Guid ProspectId { get; set; }
    public Guid StepId { get; set; }
    public Guid? CampaignId { get; set; }
    public string Variant { get; set; } = "A";
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? PromptUsed { get; set; }
    public string? Model { get; set; }
    public decimal? Temperature { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public decimal? CostUsd { get; set; }
    public bool Approved { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? MetadataJson { get; set; }

    public Prospect? Prospect { get; set; }
    public ProspectingSequenceStep? Step { get; set; }
    public ProspectingCampaign? Campaign { get; set; }
    public ICollection<SendLog> Sends { get; set; } = new List<SendLog>();
}
