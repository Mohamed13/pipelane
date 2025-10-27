using System;

namespace Pipelane.Domain.Entities.Prospecting;

public class ProspectListItem : BaseEntity
{
    public Guid ProspectListId { get; set; }
    public Guid ProspectId { get; set; }
    public DateTime AddedAtUtc { get; set; }

    public ProspectList? List { get; set; }
    public Prospect? Prospect { get; set; }
}
