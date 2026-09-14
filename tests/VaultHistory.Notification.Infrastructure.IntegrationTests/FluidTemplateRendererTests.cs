using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Infrastructure.Options;
using VaultHistory.Notification.Infrastructure.Templates;

namespace VaultHistory.Notification.Infrastructure.IntegrationTests;

public sealed class FluidTemplateRendererTests
{
    [Fact]
    public async Task Renders_history_as_safe_html_paragraphs()
    {
        using var templates = new TemplateDirectory();
        templates.Write("history.liquid", "{{ fullname }}|{{ birthDate }}|{{ storyHtml | raw }}");
        var renderer = templates.CreateRenderer();

        var result = await renderer.RenderAsync(
            EmailTemplate.History,
            new HistoryEmailTemplateModel("Ana <script>", new DateTimeOffset(2000, 1, 2, 0, 0, 0, TimeSpan.Zero), "<img src=x onerror=alert(1)>\nSegundo párrafo"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana &lt;script&gt;|2000-01-02|<p>&lt;img src=x onerror=alert(1)&gt;</p><p>Segundo p&#xE1;rrafo</p>", result.Value);
    }

    [Fact]
    public async Task Renders_sign_in_with_an_explicit_configured_time_zone()
    {
        using var templates = new TemplateDirectory();
        templates.Write("sign-in.liquid", "{{ fullname }}|{{ occurredOn }}");
        var renderer = templates.CreateRenderer();

        var result = await renderer.RenderAsync(
            EmailTemplate.SignIn,
            new SignInEmailTemplateModel("Ana", new DateTimeOffset(2026, 9, 6, 17, 30, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana|2026-09-06 12:30 (-05:00)", result.Value);
    }

    [Fact]
    public async Task Caches_a_parsed_template_after_the_first_render()
    {
        using var templates = new TemplateDirectory();
        templates.Write("welcome.liquid", "Hola, {{ fullname }}.");
        var renderer = templates.CreateRenderer();

        var first = await renderer.RenderAsync(EmailTemplate.Welcome, new WelcomeEmailTemplateModel("Ana"), CancellationToken.None);
        File.Delete(Path.Combine(templates.RootPath, "welcome.liquid"));
        var second = await renderer.RenderAsync(EmailTemplate.Welcome, new WelcomeEmailTemplateModel("Bea"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("Hola, Bea.", second.Value);
    }

    [Fact]
    public async Task Returns_controlled_errors_for_unknown_or_invalid_templates()
    {
        using var templates = new TemplateDirectory();
        templates.Write("history.liquid", "{% if %}");
        var renderer = templates.CreateRenderer();

        var invalid = await renderer.RenderAsync(EmailTemplate.History, new HistoryEmailTemplateModel("Ana", null, "Historia"), CancellationToken.None);
        var unknown = await renderer.RenderAsync((EmailTemplate)999, new WelcomeEmailTemplateModel("Ana"), CancellationToken.None);

        Assert.Equal("templates.invalid_template", invalid.Error?.Code);
        Assert.Equal("templates.unknown_template", unknown.Error?.Code);
    }

    private sealed class TemplateDirectory : IDisposable
    {
        public TemplateDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"vault-history-notification-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Write(string name, string content) => File.WriteAllText(Path.Combine(RootPath, name), content);

        public FluidTemplateRenderer CreateRenderer() => new(
            new TestHostEnvironment(RootPath),
            Microsoft.Extensions.Options.Options.Create(new TemplatesOptions
            {
                HistoryTemplatePath = "history.liquid",
                SignInTemplatePath = "sign-in.liquid",
                WelcomeTemplatePath = "welcome.liquid",
                TimeZoneId = "America/Guayaquil"
            }),
            NullLogger<FluidTemplateRenderer>.Instance);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "VaultHistory.Notification.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
