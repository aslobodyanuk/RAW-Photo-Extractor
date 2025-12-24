using FluentValidation;

namespace RAW.Photo.Extractor;

public class AppConfigValidator : AbstractValidator<AppConfig>
{
    public AppConfigValidator()
    {
        RuleFor(x => x.RawFileExtensions)
            .NotNull()
            .WithMessage("RawFileExtensions cannot be null")
            .NotEmpty()
            .WithMessage("RawFileExtensions must contain at least one extension")
            .Must(extensions => extensions.All(ext => !string.IsNullOrWhiteSpace(ext)))
            .WithMessage("All RawFileExtensions must be non-empty strings");
    }
}

