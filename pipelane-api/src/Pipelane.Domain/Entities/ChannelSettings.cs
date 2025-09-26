using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class ChannelSettings : BaseEntity
{
    public Channel Channel { get; set; }
    public string SettingsJson { get; set; } = "{}";
}

