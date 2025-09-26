using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pipelane.Application.Storage;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("conversations")] 
public class ConversationsController : ControllerBase
{
    private readonly IAppDbContext _db;
    public ConversationsController(IAppDbContext db) => _db = db;

    [HttpGet("{contactId:guid}")]
    public async Task<IActionResult> GetByContact(Guid contactId, [FromQuery] int last = 50, CancellationToken ct = default)
    {
        var convos = await _db.Conversations.Where(c => c.ContactId == contactId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(1)
            .ToListAsync(ct);
        if (convos.Count == 0) return Ok(new { messages = Array.Empty<object>() });
        var cid = convos[0].Id;
        var msgs = await _db.Messages.Where(m => m.ConversationId == cid)
            .OrderByDescending(m => m.CreatedAt)
            .Take(last)
            .ToListAsync(ct);
        return Ok(new { conversationId = cid, messages = msgs.OrderBy(m => m.CreatedAt) });
    }
}
