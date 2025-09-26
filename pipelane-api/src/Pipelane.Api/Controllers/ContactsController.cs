using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pipelane.Application.DTOs;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("contacts")] 
public class ContactsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly Pipelane.Infrastructure.Persistence.ITenantProvider _tenant;
    public ContactsController(IAppDbContext db, Pipelane.Infrastructure.Persistence.ITenantProvider tenant) { _db = db; _tenant = tenant; }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportContactsRequest req, CancellationToken ct)
    {
        var bytes = Convert.FromBase64String(req.PayloadBase64);
        var text = Encoding.UTF8.GetString(bytes);
        if (req.Kind.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var arr = JsonSerializer.Deserialize<List<Contact>>(text) ?? new();
            foreach (var c in arr) { if (c.Id == Guid.Empty) c.Id = Guid.NewGuid(); c.TenantId = _tenant.TenantId; c.CreatedAt = DateTime.UtcNow; c.UpdatedAt = c.CreatedAt; }
            await _db.Contacts.AddRangeAsync(arr, ct);
            await _db.SaveChangesAsync(ct);
            return Ok(new { imported = arr.Count });
        }
        // CSV minimal: phone,email,first,last,lang
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int count = 0;
        foreach (var line in lines.Skip(1))
        {
            var cols = line.Trim().Split(',');
            if (cols.Length < 5) continue;
            _db.Contacts.Add(new Contact
            {
                Id = Guid.NewGuid(),
                TenantId = _tenant.TenantId,
                Phone = cols[0],
                Email = cols[1],
                FirstName = cols[2],
                LastName = cols[3],
                Lang = cols[4],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            count++;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { imported = count });
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var q = _db.Contacts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(c => (c.Phone ?? "").Contains(search) || (c.Email ?? "").Contains(search) || (c.FirstName ?? "").Contains(search));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(c => c.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Ok(new { total, items });
    }
}
