using FluentValidation;
using Pipelane.Application.DTOs;

namespace Pipelane.Application.Validators;

public sealed class CreateCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.SegmentJson).NotEmpty();
    }
}

