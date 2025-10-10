using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.DTOs;
using Pipelane.Application.Storage;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/followups")]
public sealed class FollowupsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public FollowupsController(IAppDbContext db)
    {
        _db = db;
    }

    [HttpPost("preview")]
    public async Task<ActionResult<object>> Preview([FromBody] FollowupPreviewRequest request, CancellationToken cancellationToken)
    {
        var query = _db.Contacts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SegmentJson))
        {
            Dictionary<string, string>? segment;
            try
            {
                segment = JsonSerializer.Deserialize<Dictionary<string, string>>(request.SegmentJson, _jsonOptions);
            }
            catch (JsonException)
            {
                return BadRequest("SegmentJson is not valid JSON");
            }

            if (segment is not null)
            {
                if (segment.TryGetValue("lang", out var lang) && !string.IsNullOrWhiteSpace(lang))
                {
                    query = query.Where(c => c.Lang == lang);
                }

                if (segment.TryGetValue("tag", out var tag) && !string.IsNullOrWhiteSpace(tag))
                {
                    query = query.Where(c => c.TagsJson != null && c.TagsJson.Contains(tag));
                }
            }
        }

        var count = await query.CountAsync(cancellationToken);
        return Ok(new { count });
    }
}
