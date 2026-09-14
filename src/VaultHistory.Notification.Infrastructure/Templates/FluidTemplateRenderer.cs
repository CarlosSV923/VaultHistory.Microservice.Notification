using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Encodings.Web;
using Fluid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.Templates;

public sealed class FluidTemplateRenderer : ITemplateRenderer
{
    private static readonly FluidParser Parser = new();
    private static readonly TemplateOptions TemplateOptions = new() { StrictVariables = true };
    private readonly ConcurrentDictionary<EmailTemplate, IFluidTemplate> templateCache = new();
    private readonly TemplatesOptions options;
    private readonly string contentRootPath;
    private readonly ILogger<FluidTemplateRenderer> logger;

    public FluidTemplateRenderer(
        IHostEnvironment environment,
        IOptions<TemplatesOptions> options,
        ILogger<FluidTemplateRenderer> logger)
    {
        this.options = options.Value;
        contentRootPath = Path.GetFullPath(environment.ContentRootPath);
        this.logger = logger;
    }

    public async Task<Result<string>> RenderAsync(
        EmailTemplate template,
        EmailTemplateModel model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Fullname))
        {
            return Result<string>.Failure(new Error("templates.invalid_model", "The recipient full name is required."));
        }

        if (model is HistoryEmailTemplateModel { Story: null or "" })
        {
            return Result<string>.Failure(new Error("templates.invalid_model", "A history email requires generated content."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fluidTemplate = await GetTemplateAsync(template, cancellationToken);
            var context = new TemplateContext(CreateViewModel(model), TemplateOptions, allowModelMembers: true);
            var html = await fluidTemplate.RenderAsync(context, HtmlEncoder.Default, isolateContext: true);
            cancellationToken.ThrowIfCancellationRequested();
            return Result<string>.Success(html);
        }
        catch (TemplateRenderException exception)
        {
            logger.LogWarning(exception, "Unable to render email template {Template}.", template);
            return Result<string>.Failure(new Error(exception.Code, exception.Message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unexpected error rendering email template {Template}.", template);
            return Result<string>.Failure(new Error("templates.render_failed", "The email template could not be rendered."));
        }
    }

    private async Task<IFluidTemplate> GetTemplateAsync(EmailTemplate template, CancellationToken cancellationToken)
    {
        if (templateCache.TryGetValue(template, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        var path = ResolveTemplatePath(template);
        string source;

        try
        {
            source = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (FileNotFoundException exception)
        {
            throw new TemplateRenderException("templates.not_found", $"The template file '{Path.GetFileName(path)}' was not found.", exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new TemplateRenderException("templates.not_found", $"The template directory for '{Path.GetFileName(path)}' was not found.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new TemplateRenderException("templates.unavailable", $"The template file '{Path.GetFileName(path)}' cannot be read.", exception);
        }

        if (!Parser.TryParse(source, out var parsedTemplate, out var error))
        {
            throw new TemplateRenderException("templates.invalid_template", $"The template '{Path.GetFileName(path)}' is invalid: {error}");
        }

        return templateCache.GetOrAdd(template, parsedTemplate);
    }

    private string ResolveTemplatePath(EmailTemplate template)
    {
        var configuredPath = template switch
        {
            EmailTemplate.History => options.HistoryTemplatePath,
            EmailTemplate.SignIn => options.SignInTemplatePath,
            EmailTemplate.Welcome => options.WelcomeTemplatePath,
            _ => throw new TemplateRenderException("templates.unknown_template", $"The template '{template}' is not supported.")
        };

        var templatePath = Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
        var rootWithSeparator = contentRootPath.EndsWith(Path.DirectorySeparatorChar)
            ? contentRootPath
            : contentRootPath + Path.DirectorySeparatorChar;

        if (!templatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new TemplateRenderException("templates.invalid_path", "The template path must be within the application content root.");
        }

        return templatePath;
    }

    private object CreateViewModel(EmailTemplateModel model) => model switch
    {
        HistoryEmailTemplateModel history => new
        {
            fullname = history.Fullname,
            birthDate = history.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            storyHtml = ToSafeParagraphs(history.Story)
        },
        SignInEmailTemplateModel signIn => new
        {
            fullname = signIn.Fullname,
            occurredOn = FormatInConfiguredTimeZone(signIn.OccurredOn)
        },
        WelcomeEmailTemplateModel welcome => new { fullname = welcome.Fullname },
        _ => throw new TemplateRenderException("templates.invalid_model", "The email model is not supported.")
    };

    private string FormatInConfiguredTimeZone(DateTimeOffset occurredOn)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
            var localTime = TimeZoneInfo.ConvertTime(occurredOn, timeZone);
            return $"{localTime:yyyy-MM-dd HH:mm} ({localTime:zzz})";
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new TemplateRenderException("templates.invalid_timezone", $"The time zone '{options.TimeZoneId}' is not available.", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new TemplateRenderException("templates.invalid_timezone", $"The time zone '{options.TimeZoneId}' is invalid.", exception);
        }
    }

    private static string ToSafeParagraphs(string story) => string.Concat(
        story.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .Select(paragraph => $"<p>{HtmlEncoder.Default.Encode(paragraph.Trim())}</p>"));

    private sealed class TemplateRenderException(string code, string message, Exception? innerException = null)
        : Exception(message, innerException)
    {
        public string Code { get; } = code;
    }
}
