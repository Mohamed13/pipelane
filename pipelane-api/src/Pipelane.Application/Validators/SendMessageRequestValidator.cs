using FluentValidation;

using Pipelane.Application.DTOs;

namespace Pipelane.Application.Validators;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Type).NotEmpty();
        When(x => string.Equals(x.Type, "text", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Text).NotEmpty();
        });
        When(x => string.Equals(x.Type, "template", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.TemplateName).NotEmpty();
        });
        RuleFor(x => x).Must(x => x.ContactId.HasValue || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Provide contactId or phone");
    }
}

