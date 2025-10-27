using System;

namespace Pipelane.Application.DTOs;

public class FollowupPreviewRequest
{
    public string? SegmentJson { get; set; }
    public Guid? ConversationId { get; set; }
}
