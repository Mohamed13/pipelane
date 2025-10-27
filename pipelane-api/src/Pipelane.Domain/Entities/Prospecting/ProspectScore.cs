using System;

namespace Pipelane.Domain.Entities.Prospecting;

public class ProspectScore : BaseEntity
{
    public Guid ProspectId { get; set; }
    public int Score { get; set; }
    public string FeaturesJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; }

    public Prospect? Prospect { get; set; }
}
