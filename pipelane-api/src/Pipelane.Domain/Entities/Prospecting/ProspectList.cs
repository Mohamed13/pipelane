using System;
using System.Collections.Generic;

namespace Pipelane.Domain.Entities.Prospecting;

public class ProspectList : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<ProspectListItem> Items { get; set; } = new List<ProspectListItem>();
}
