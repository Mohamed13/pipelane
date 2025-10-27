using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Prospecting;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Application.Prospecting;

public interface IProspectingService
{
    Task<ProspectImportResult> ImportProspectsAsync(Guid tenantId, ProspectImportRequest request, CancellationToken ct);
    Task<(int Total, IReadOnlyList<ProspectDto> Items)> GetProspectsAsync(int page, int size, string? search, CancellationToken ct);
    Task<ProspectingSequenceDto> CreateSequenceAsync(Guid tenantId, ProspectingSequenceCreateRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProspectingSequenceDto>> GetSequencesAsync(CancellationToken ct);
    Task<ProspectingSequenceDto> UpdateSequenceAsync(Guid sequenceId, ProspectingSequenceUpdateRequest request, CancellationToken ct);
    Task DeleteSequenceAsync(Guid sequenceId, CancellationToken ct);
    Task<ProspectingCampaignDto> CreateCampaignAsync(Guid tenantId, ProspectingCampaignCreateRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProspectingCampaignDto>> GetCampaignsAsync(CancellationToken ct);
    Task<ProspectingCampaignDto?> GetCampaignAsync(Guid campaignId, CancellationToken ct);
    Task<ProspectingCampaignDto> StartCampaignAsync(Guid campaignId, CancellationToken ct);
    Task<ProspectingCampaignDto> PauseCampaignAsync(Guid campaignId, CancellationToken ct);
    Task<ProspectingCampaignPreview> PreviewCampaignAsync(Guid campaignId, CancellationToken ct);
    Task<ProspectingAnalyticsResponse> GetAnalyticsAsync(DateTime from, DateTime to, CancellationToken ct);
    Task<GenerateEmailResponse> GenerateEmailAsync(Guid tenantId, GenerateEmailRequest request, CancellationToken ct);
    Task<ProspectingClassifyReplyResponse> ClassifyReplyAsync(ProspectingClassifyReplyRequest request, CancellationToken ct);
    Task<AutoReplyResponse> AutoReplyAsync(Guid tenantId, AutoReplyRequest request, CancellationToken ct);
    Task<ProspectDto?> UpdateOptOutAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<ProspectReplyDto>> GetRepliesAsync(ReplyIntent? intent, CancellationToken ct);
}

public sealed class ProspectingService : IProspectingService
{
    private readonly IAppDbContext _db;
    private readonly IProspectingAiService _ai;
    private readonly ILogger<ProspectingService> _logger;

    public ProspectingService(IAppDbContext db, IProspectingAiService ai, ILogger<ProspectingService> logger)
    {
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProspectImportResult> ImportProspectsAsync(Guid tenantId, ProspectImportRequest request, CancellationToken ct)
    {
        var imported = 0;
        var skipped = 0;
        var updated = 0;
        var now = DateTime.UtcNow;
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(request.PayloadBase64));
        var canonicalFields = BuildFieldMap(request.FieldMap);
        var existing = await _db.Prospects
            .Where(p => p.Email != null)
            .ToDictionaryAsync(p => p.Email!.ToLowerInvariant(), p => p, ct);

        if (request.Kind.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("JSON payload must be an array of prospects.");
            }

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var values = ExtractValues(element, canonicalFields);
                ProcessRow(values, tenantId, now, request.OverwriteExisting, existing, ref imported, ref skipped, ref updated);
            }
        }
        else
        {
            var lines = payload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1)
            {
                return new ProspectImportResult(imported, skipped, updated);
            }

            var headers = ParseCsvLine(lines[0]);
            var mapper = BuildHeaderMap(headers, canonicalFields);

            foreach (var line in lines.Skip(1))
            {
                var columns = ParseCsvLine(line);
                var values = MapColumns(columns, mapper);
                ProcessRow(values, tenantId, now, request.OverwriteExisting, existing, ref imported, ref skipped, ref updated);
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ProspectImportResult(imported, skipped, updated);
    }

    /// <inheritdoc/>
    public async Task<(int Total, IReadOnlyList<ProspectDto> Items)> GetProspectsAsync(int page, int size, string? search, CancellationToken ct)
    {
        var query = _db.Prospects.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Email.ToLower()!.Contains(pattern) ||
                (p.FirstName ?? string.Empty).ToLower().Contains(pattern) ||
                (p.LastName ?? string.Empty).ToLower().Contains(pattern) ||
                (p.Company ?? string.Empty).ToLower().Contains(pattern));
        }

        var take = Math.Clamp(size, 10, 200);
        var skip = Math.Max(0, page - 1) * take;
        var total = await query.CountAsync(ct);

        var prospects = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (total, prospects.Select(p => p.ToDto()).ToList());
    }

    /// <inheritdoc/>
    public async Task<ProspectingSequenceDto> CreateSequenceAsync(Guid tenantId, ProspectingSequenceCreateRequest request, CancellationToken ct)
    {
        if (request.Steps.Count == 0)
        {
            throw new InvalidOperationException("A sequence must include at least one step.");
        }

        var now = DateTime.UtcNow;
        var sequence = new ProspectingSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            TargetPersona = request.TargetPersona,
            EntryCriteriaJson = request.EntryCriteriaJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Steps = new List<ProspectingSequenceStep>()
        };

        for (var index = 0; index < request.Steps.Count; index++)
        {
            var input = request.Steps[index];
            sequence.Steps.Add(new ProspectingSequenceStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SequenceId = sequence.Id,
                Order = index,
                StepType = input.StepType,
                Channel = input.Channel,
                OffsetDays = input.OffsetDays,
                SendWindowStartUtc = input.SendWindowStartUtc,
                SendWindowEndUtc = input.SendWindowEndUtc,
                PromptTemplate = input.PromptTemplate,
                SubjectTemplate = input.SubjectTemplate,
                GuardrailInstructions = input.GuardrailInstructions,
                RequiresApproval = input.RequiresApproval,
                MetadataJson = input.MetadataJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await _db.ProspectingSequences.AddAsync(sequence, ct);
        await _db.SaveChangesAsync(ct);
        return sequence.ToDto();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProspectingSequenceDto>> GetSequencesAsync(CancellationToken ct)
    {
        var sequences = await _db.ProspectingSequences
            .Include(s => s.Steps)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(ct);
        return sequences.Select(s => s.ToDto()).ToList();
    }

    /// <inheritdoc/>
    public async Task<ProspectingSequenceDto> UpdateSequenceAsync(Guid sequenceId, ProspectingSequenceUpdateRequest request, CancellationToken ct)
    {
        var sequence = await _db.ProspectingSequences
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.Id == sequenceId, ct);

        if (sequence is null)
        {
            throw new KeyNotFoundException("Sequence not found.");
        }

        sequence.Name = request.Name;
        sequence.Description = request.Description;
        sequence.IsActive = request.IsActive;
        sequence.TargetPersona = request.TargetPersona;
        sequence.EntryCriteriaJson = request.EntryCriteriaJson;
        sequence.UpdatedAtUtc = DateTime.UtcNow;

        var existingSteps = sequence.Steps.ToList();
        _db.ProspectingSequenceSteps.RemoveRange(existingSteps);

        for (var index = 0; index < request.Steps.Count; index++)
        {
            var input = request.Steps[index];
            sequence.Steps.Add(new ProspectingSequenceStep
            {
                Id = Guid.NewGuid(),
                TenantId = sequence.TenantId,
                SequenceId = sequence.Id,
                Order = index,
                StepType = input.StepType,
                Channel = input.Channel,
                OffsetDays = input.OffsetDays,
                SendWindowStartUtc = input.SendWindowStartUtc,
                SendWindowEndUtc = input.SendWindowEndUtc,
                PromptTemplate = input.PromptTemplate,
                SubjectTemplate = input.SubjectTemplate,
                GuardrailInstructions = input.GuardrailInstructions,
                RequiresApproval = input.RequiresApproval,
                MetadataJson = input.MetadataJson,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return sequence.ToDto();
    }

    /// <inheritdoc/>
    public async Task DeleteSequenceAsync(Guid sequenceId, CancellationToken ct)
    {
        var sequence = await _db.ProspectingSequences.FirstOrDefaultAsync(s => s.Id == sequenceId, ct);
        if (sequence == null)
        {
            return;
        }

        var hasCampaign = await _db.ProspectingCampaigns.AnyAsync(c => c.SequenceId == sequenceId, ct);
        if (hasCampaign)
        {
            throw new InvalidOperationException("Cannot delete sequence linked to campaigns.");
        }

        _db.ProspectingSequences.Remove(sequence);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<ProspectingCampaignDto> CreateCampaignAsync(Guid tenantId, ProspectingCampaignCreateRequest request, CancellationToken ct)
    {
        var sequenceExists = await _db.ProspectingSequences.AnyAsync(s => s.Id == request.SequenceId, ct);
        if (!sequenceExists)
        {
            throw new KeyNotFoundException("Sequence not found.");
        }

        var now = DateTime.UtcNow;
        var campaign = new ProspectingCampaign
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SequenceId = request.SequenceId,
            Name = request.Name,
            Status = ProspectingCampaignStatus.Draft,
            SegmentJson = string.IsNullOrWhiteSpace(request.SegmentJson) ? "{}" : request.SegmentJson,
            SettingsJson = request.SettingsJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ScheduledAtUtc = request.ScheduledAtUtc
        };

        await _db.ProspectingCampaigns.AddAsync(campaign, ct);
        await _db.SaveChangesAsync(ct);
        return campaign.ToDto();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProspectingCampaignDto>> GetCampaignsAsync(CancellationToken ct)
    {
        var campaigns = await _db.ProspectingCampaigns
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        return campaigns.Select(c => c.ToDto()).ToList();
    }

    /// <inheritdoc/>
    public async Task<ProspectingCampaignDto?> GetCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        var campaign = await _db.ProspectingCampaigns
            .Include(c => c.Sequence)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        return campaign?.ToDto();
    }

    /// <inheritdoc/>
    public async Task<ProspectingCampaignDto> StartCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        var campaign = await _db.ProspectingCampaigns
            .Include(c => c.Sequence!)
                .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);

        if (campaign is null)
        {
            throw new KeyNotFoundException("Campaign not found.");
        }

        if (campaign.Status == ProspectingCampaignStatus.Running)
        {
            return campaign.ToDto();
        }

        var prospects = await _db.Prospects
            .Where(p => !p.OptedOut && (p.CampaignId == null || p.CampaignId == campaign.Id))
            .ToListAsync(ct);

        if (prospects.Count == 0)
        {
            throw new InvalidOperationException("No prospects available for this campaign.");
        }

        var steps = campaign.Sequence?.Steps.OrderBy(s => s.Order).ToList() ?? new List<ProspectingSequenceStep>();
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("Sequence has no steps configured.");
        }

        var startDate = campaign.ScheduledAtUtc ?? DateTime.UtcNow;

        foreach (var prospect in prospects)
        {
            prospect.CampaignId = campaign.Id;
            prospect.SequenceId = campaign.SequenceId;
            prospect.Status = ProspectStatus.Scheduled;
            prospect.UpdatedAtUtc = DateTime.UtcNow;

            foreach (var step in steps)
            {
                var scheduled = startDate.Date.AddDays(step.OffsetDays);
                if (step.SendWindowStartUtc.HasValue)
                {
                    scheduled = scheduled.Date + step.SendWindowStartUtc.Value;
                }
                else
                {
                    scheduled = scheduled.AddHours(14); // default 2pm UTC window
                }

                if (scheduled.Hour < 8)
                {
                    scheduled = scheduled.Date.AddHours(8);
                }
                else if (scheduled.Hour > 18)
                {
                    scheduled = scheduled.Date.AddDays(1).AddHours(8);
                }

                var sendLog = new SendLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = campaign.TenantId,
                    ProspectId = prospect.Id,
                    CampaignId = campaign.Id,
                    StepId = step.Id,
                    Provider = "sendgrid",
                    Status = SendLogStatus.Scheduled,
                    ScheduledAtUtc = scheduled,
                    CreatedAtUtc = DateTime.UtcNow,
                    MetadataJson = "{}"
                };
                await _db.ProspectingSendLogs.AddAsync(sendLog, ct);
            }
        }

        campaign.Status = ProspectingCampaignStatus.Running;
        campaign.StartedAtUtc = DateTime.UtcNow;
        campaign.UpdatedAtUtc = campaign.StartedAtUtc.Value;

        await _db.SaveChangesAsync(ct);
        return campaign.ToDto();
    }

    /// <inheritdoc/>
    public async Task<ProspectingCampaignDto> PauseCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        var campaign = await _db.ProspectingCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign is null)
        {
            throw new KeyNotFoundException("Campaign not found.");
        }

        campaign.Status = ProspectingCampaignStatus.Paused;
        campaign.PausedAtUtc = DateTime.UtcNow;
        campaign.UpdatedAtUtc = campaign.PausedAtUtc.Value;

        var scheduled = await _db.ProspectingSendLogs
            .Where(l => l.CampaignId == campaignId && l.Status == SendLogStatus.Scheduled)
            .ToListAsync(ct);
        foreach (var log in scheduled)
        {
            log.Status = SendLogStatus.Cancelled;
            log.ErrorReason = "Campaign paused";
            log.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return campaign.ToDto();
    }

    /// <inheritdoc/>
    public async Task<ProspectingCampaignPreview> PreviewCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        var campaign = await _db.ProspectingCampaigns
            .Include(c => c.Sequence!)
                .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign is null)
        {
            throw new KeyNotFoundException("Campaign not found.");
        }

        var steps = campaign.Sequence?.Steps.OrderBy(s => s.Order).ToList() ?? new List<ProspectingSequenceStep>();
        var prospects = await _db.Prospects.CountAsync(p => p.CampaignId == campaignId || p.CampaignId == null, ct);
        var startDate = campaign.ScheduledAtUtc ?? DateTime.UtcNow;

        var schedule = steps.Select(step =>
        {
            var scheduled = startDate.Date.AddDays(step.OffsetDays);
            if (step.SendWindowStartUtc.HasValue)
            {
                scheduled = scheduled.Date + step.SendWindowStartUtc.Value;
            }
            else
            {
                scheduled = scheduled.AddHours(14);
            }

            return new ProspectingSchedulePreviewStep(step.Id, scheduled, $"{step.Order + 1}. {step.StepType} ({step.Channel})");
        }).ToList();

        return new ProspectingCampaignPreview(campaign.Id, prospects, schedule);
    }

    /// <inheritdoc/>
    public async Task<ProspectingAnalyticsResponse> GetAnalyticsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var totalProspects = await _db.Prospects.CountAsync(ct);
        var activeCampaigns = await _db.ProspectingCampaigns.CountAsync(c => c.Status == ProspectingCampaignStatus.Running, ct);
        var sendLogs = await _db.ProspectingSendLogs
            .Where(l => l.ScheduledAtUtc >= from && l.ScheduledAtUtc <= to)
            .ToListAsync(ct);
        var replies = await _db.ProspectReplies
            .Where(r => r.ReceivedAtUtc >= from && r.ReceivedAtUtc <= to)
            .ToListAsync(ct);

        var emailsSent = sendLogs.Count(l => l.SentAtUtc != null || l.Status == SendLogStatus.Sent || l.Status == SendLogStatus.Delivered || l.Status == SendLogStatus.Opened);
        var emailsOpened = sendLogs.Count(l => l.OpenedAtUtc != null || l.Status == SendLogStatus.Opened);
        var repliesReceived = replies.Count;
        var meetingsBooked = replies.Count(r => r.Intent == ReplyIntent.Interested || r.Intent == ReplyIntent.MeetingRequested);

        var stepIds = sendLogs
            .Where(l => l.StepId.HasValue)
            .Select(l => l.StepId!.Value)
            .Distinct()
            .ToList();
        var stepLookup = stepIds.Count == 0
            ? new Dictionary<Guid, ProspectingSequenceStep>()
            : await _db.ProspectingSequenceSteps
                .Where(s => stepIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);

        var dailySeries = sendLogs
            .GroupBy(l => l.ScheduledAtUtc.Date)
            .Select(g =>
            {
                var day = g.Key;
                var sent = g.Count(l => l.SentAtUtc != null || l.Status == SendLogStatus.Sent || l.Status == SendLogStatus.Delivered);
                var opened = g.Count(l => l.OpenedAtUtc != null || l.Status == SendLogStatus.Opened);
                var dayReplies = replies.Count(r => r.ReceivedAtUtc.Date == day);
                var meetings = replies.Count(r => r.ReceivedAtUtc.Date == day && (r.Intent == ReplyIntent.Interested || r.Intent == ReplyIntent.MeetingRequested));
                return new ProspectingSeriesPoint(day, sent, opened, dayReplies, meetings);
            })
            .OrderBy(p => p.Date)
            .ToList();

        var stepBreakdown = sendLogs
            .Where(l => l.StepId != null)
            .GroupBy(l => l.StepId!.Value)
            .Select(g =>
            {
                var sent = g.Count(l => l.SentAtUtc != null || l.Status == SendLogStatus.Sent || l.Status == SendLogStatus.Delivered);
                var stepReplies = replies.Count(r => r.StepId == g.Key);
                var meetings = replies.Count(r => r.StepId == g.Key && (r.Intent == ReplyIntent.Interested || r.Intent == ReplyIntent.MeetingRequested));
                var label = stepLookup.TryGetValue(g.Key, out var step)
                    ? $"{step.Order + 1}. {step.StepType} ({step.Channel})"
                    : $"Step {g.Key.ToString()[..8]}";
                return new ProspectingStepSeriesPoint(g.Key, label, sent, stepReplies, meetings);
            })
            .ToList();

        return new ProspectingAnalyticsResponse(
            totalProspects,
            activeCampaigns,
            emailsSent,
            emailsOpened,
            repliesReceived,
            meetingsBooked,
            dailySeries,
            stepBreakdown);
    }

    /// <inheritdoc/>
    public async Task<GenerateEmailResponse> GenerateEmailAsync(Guid tenantId, GenerateEmailRequest request, CancellationToken ct)
    {
        var prospect = await _db.Prospects.FirstOrDefaultAsync(p => p.Id == request.ProspectId, ct);
        if (prospect is null)
        {
            throw new KeyNotFoundException("Prospect not found.");
        }

        var step = await _db.ProspectingSequenceSteps.FirstOrDefaultAsync(s => s.Id == request.StepId, ct);
        if (step is null)
        {
            throw new KeyNotFoundException("Sequence step not found.");
        }

        var campaign = request.CampaignId.HasValue
            ? await _db.ProspectingCampaigns.FirstOrDefaultAsync(c => c.Id == request.CampaignId, ct)
            : null;

        var (subject, html, text, promptTokens, completionTokens, costUsd) =
            await _ai.GenerateEmailAsync(prospect, step, campaign, ct);

        var generation = new EmailGeneration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProspectId = prospect.Id,
            StepId = step.Id,
            CampaignId = campaign?.Id,
            Variant = request.Variant,
            Subject = subject,
            HtmlBody = html,
            TextBody = text,
            PromptUsed = step.PromptTemplate,
            Model = "gpt-4o-mini",
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            CostUsd = costUsd,
            Approved = !step.RequiresApproval,
            CreatedAtUtc = DateTime.UtcNow,
            MetadataJson = "{}"
        };

        await _db.EmailGenerations.AddAsync(generation, ct);
        await _db.SaveChangesAsync(ct);
        return new GenerateEmailResponse(generation.Id, subject, html, text, generation.Variant, promptTokens, completionTokens, costUsd);
    }

    /// <inheritdoc/>
    public async Task<ProspectingClassifyReplyResponse> ClassifyReplyAsync(ProspectingClassifyReplyRequest request, CancellationToken ct)
    {
        var reply = await _db.ProspectReplies.FirstOrDefaultAsync(r => r.Id == request.ReplyId, ct);
        if (reply is null)
        {
            throw new KeyNotFoundException("Reply not found.");
        }

        var (intent, confidence, datesJson) = await _ai.ClassifyReplyAsync(reply, ct);
        reply.Intent = intent;
        reply.IntentConfidence = confidence;
        reply.ExtractedDatesJson = datesJson;
        reply.ProcessedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new ProspectingClassifyReplyResponse(reply.Id, intent, confidence, datesJson);
    }

    /// <inheritdoc/>
    public async Task<AutoReplyResponse> AutoReplyAsync(Guid tenantId, AutoReplyRequest request, CancellationToken ct)
    {
        var reply = await _db.ProspectReplies
            .Include(r => r.Prospect)
            .FirstOrDefaultAsync(r => r.Id == request.ReplyId, ct);
        if (reply is null)
        {
            throw new KeyNotFoundException("Reply not found.");
        }

        if (reply.Prospect is null)
        {
            throw new InvalidOperationException("Reply is missing associated prospect.");
        }

        var (subject, html, text, promptTokens, completionTokens, costUsd) =
            await _ai.CreateAutoReplyDraftAsync(reply, reply.Prospect, ct);

        var generation = new EmailGeneration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProspectId = reply.ProspectId,
            CampaignId = reply.CampaignId,
            StepId = reply.StepId ?? Guid.Empty,
            Variant = "auto_reply",
            Subject = subject,
            HtmlBody = html,
            TextBody = text,
            PromptUsed = "auto-reply",
            Model = "gpt-4o-mini",
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            CostUsd = costUsd,
            Approved = true,
            CreatedAtUtc = DateTime.UtcNow,
            MetadataJson = "{}"
        };

        await _db.EmailGenerations.AddAsync(generation, ct);
        reply.AutoReplyGenerationId = generation.Id;
        reply.AutoReplySuggested = true;
        await _db.SaveChangesAsync(ct);

        return new AutoReplyResponse(generation.Id, subject, html, text, generation.Variant);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProspectReplyDto>> GetRepliesAsync(ReplyIntent? intent, CancellationToken ct)
    {
        var query = _db.ProspectReplies
            .Include(r => r.Prospect)
            .OrderByDescending(r => r.ReceivedAtUtc)
            .AsQueryable();

        if (intent.HasValue)
        {
            query = query.Where(r => r.Intent == intent.Value);
        }

        var replies = await query.Take(200).ToListAsync(ct);
        return replies.Select(r => r.ToDto()).ToList();
    }

    /// <inheritdoc/>
    public async Task<ProspectDto?> UpdateOptOutAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var prospect = await _db.Prospects.FirstOrDefaultAsync(p => p.Email.ToLower() == normalized, ct);
        if (prospect is null)
        {
            return null;
        }

        prospect.OptedOut = true;
        prospect.OptedOutAtUtc = DateTime.UtcNow;
        prospect.Status = ProspectStatus.OptedOut;
        prospect.UpdatedAtUtc = prospect.OptedOutAtUtc.Value;
        await _db.SaveChangesAsync(ct);
        return prospect.ToDto();
    }

    private void ProcessRow(
        IDictionary<string, string> values,
        Guid tenantId,
        DateTime now,
        bool overwrite,
        IDictionary<string, Prospect> existing,
        ref int imported,
        ref int skipped,
        ref int updated)
    {
        if (!values.TryGetValue("email", out var email) || string.IsNullOrWhiteSpace(email))
        {
            skipped++;
            return;
        }

        email = email.Trim().ToLowerInvariant();
        if (existing.TryGetValue(email, out var existingProspect))
        {
            if (!overwrite)
            {
                skipped++;
                return;
            }

            ApplyValues(existingProspect, values);
            existingProspect.UpdatedAtUtc = now;
            updated++;
            return;
        }

        var prospect = new Prospect
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Status = ProspectStatus.New
        };
        ApplyValues(prospect, values);
        _db.Prospects.Add(prospect);
        existing[email] = prospect;
        imported++;
    }

    private static IDictionary<string, string> ExtractValues(JsonElement element, IDictionary<string, string> fieldMap)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonical in CanonicalFields)
        {
            if (!fieldMap.TryGetValue(canonical, out var source))
            {
                source = canonical;
            }

            if (element.TryGetProperty(source, out var property) && property.ValueKind != JsonValueKind.Null)
            {
                dict[canonical] = property.ToString();
            }
            else if (element.TryGetProperty(source, StringComparison.OrdinalIgnoreCase, out var ciProperty))
            {
                dict[canonical] = ciProperty.ToString();
            }
        }

        return dict;
    }

    private static IDictionary<string, string> MapColumns(string[] columns, IDictionary<int, string> mapper)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Length; i++)
        {
            if (mapper.TryGetValue(i, out var canonical))
            {
                dict[canonical] = columns[i];
            }
        }
        return dict;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }
        values.Add(sb.ToString().Trim());
        return values.ToArray();
    }

    private static IDictionary<string, string> BuildFieldMap(IDictionary<string, string>? supplied)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonical in CanonicalFields)
        {
            dict[canonical] = canonical;
        }

        if (supplied != null)
        {
            foreach (var kvp in supplied)
            {
                dict[kvp.Key] = kvp.Value;
            }
        }

        return dict;
    }

    private static IDictionary<int, string> BuildHeaderMap(string[] headers, IDictionary<string, string> map)
    {
        var dict = new Dictionary<int, string>();
        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim();
            foreach (var canonical in map.Keys)
            {
                if (string.Equals(map[canonical], header, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(canonical, header, StringComparison.OrdinalIgnoreCase))
                {
                    dict[i] = canonical;
                    break;
                }
            }
        }
        return dict;
    }

    private static void ApplyValues(Prospect prospect, IDictionary<string, string> values)
    {
        if (values.TryGetValue("email", out var email))
        {
            prospect.Email = email.Trim().ToLowerInvariant();
        }

        if (values.TryGetValue("firstName", out var first))
        {
            prospect.FirstName = first;
        }

        if (values.TryGetValue("lastName", out var last))
        {
            prospect.LastName = last;
        }

        if (values.TryGetValue("company", out var company))
        {
            prospect.Company = company;
        }

        if (values.TryGetValue("title", out var title))
        {
            prospect.Title = title;
        }

        if (values.TryGetValue("phone", out var phone))
        {
            prospect.Phone = phone;
        }

        if (values.TryGetValue("persona", out var persona))
        {
            prospect.Persona = persona;
        }

        if (values.TryGetValue("industry", out var industry))
        {
            prospect.Industry = industry;
        }

        if (values.TryGetValue("source", out var source))
        {
            prospect.Source = source;
        }

        if (values.TryGetValue("tags", out var tags))
        {
            prospect.TagsJson = JsonSerializer.Serialize(tags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    private static readonly string[] CanonicalFields = new[]
    {
        "email",
        "firstName",
        "lastName",
        "company",
        "title",
        "phone",
        "persona",
        "industry",
        "source",
        "tags"
    };
}

file static class JsonElementExtensions
{
    public static bool TryGetProperty(this JsonElement element, string propertyName, StringComparison comparison, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, comparison))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

