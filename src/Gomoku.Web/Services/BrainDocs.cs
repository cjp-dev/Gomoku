using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Gomoku.Web.Services;

/// <summary>
/// The brain documentation (docs/brain, copied to wwwroot/content/brain by the build) rendered as HTML.
/// The README is the front page, and its links to chapter files determine chapters and their navigation order.
/// </summary>
public sealed partial class BrainDocs(HttpClient http)
{
    private const string ContentPath = "content/brain/";
    private const string Route = "docs";

    private static readonly Uri RepositoryFolder = new("https://github.com/clauspedersen/gomoku/blob/main/docs/brain/");

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private readonly Dictionary<string, Task<DocsPage>> _pages = [];
    private Task<string>? _readme;
    private Task<IReadOnlyList<DocsChapter>>? _chapters;

    public static string PageRoute(string slug) => slug.Length == 0 ? Route : $"{Route}/{slug}";

    public Task<IReadOnlyList<DocsChapter>> GetChaptersAsync() => _chapters ??= LoadChaptersAsync();

    public async Task<DocsPage?> GetPageAsync(string slug)
    {
        if (slug.Length > 0 && !(await GetChaptersAsync()).Any(chapter => chapter.Slug == slug))
        {
            return null;
        }

        if (!_pages.TryGetValue(slug, out Task<DocsPage>? page) || page.IsFaulted)
        {
            page = LoadPageAsync(slug);
            _pages[slug] = page;
        }

        return await page;
    }

    private Task<string> ReadmeAsync()
    {
        if (_readme is null || _readme.IsFaulted)
        {
            _readme = http.GetStringAsync(ContentPath + "README.md");
        }

        return _readme;
    }

    private async Task<IReadOnlyList<DocsChapter>> LoadChaptersAsync()
    {
        try
        {
            MarkdownDocument readme = Markdown.Parse(await ReadmeAsync(), Pipeline);
            return readme.Descendants<LinkInline>()
                .Where(link => !link.IsImage && link.Url is not null && ChapterFile().IsMatch(link.Url))
                .Select(link => new DocsChapter(ChapterFile().Match(link.Url!).Groups[1].Value, PlainText(link)))
                .DistinctBy(chapter => chapter.Slug)
                .ToList();
        }
        catch
        {
            _chapters = null;
            throw;
        }
    }

    private async Task<DocsPage> LoadPageAsync(string slug)
    {
        string markdown = slug.Length == 0 ? await ReadmeAsync() : await http.GetStringAsync($"{ContentPath}{slug}.md");
        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);
        RewriteLinks(document, slug);
        string title = document.Descendants<HeadingBlock>().FirstOrDefault(heading => heading.Level == 1)?.Inline is { } heading
            ? PlainText(heading)
            : "Gomoku .NET Architecture Manual";
        return new DocsPage(slug, title, document.ToHtml(Pipeline));
    }

    private static void RewriteLinks(MarkdownDocument document, string slug)
    {
        foreach (LinkInline link in document.Descendants<LinkInline>())
        {
            if (link.Url is not { Length: > 0 } url)
            {
                continue;
            }

            link.Url = RewriteUrl(url, slug, link.IsImage, out bool external);
            if (external)
            {
                HtmlAttributes attributes = link.GetAttributes();
                attributes.AddPropertyIfNotExist("target", "_blank");
                attributes.AddPropertyIfNotExist("rel", "noopener");
            }
        }
    }

    private static string RewriteUrl(string url, string slug, bool image, out bool external)
    {
        external = false;
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute) && absolute.Scheme is "http" or "https" or "mailto")
        {
            external = true;
            return url;
        }

        if (url.StartsWith('#'))
        {
            return PageRoute(slug) + url;
        }

        int hash = url.IndexOf('#');
        string path = hash < 0 ? url : url[..hash];
        string fragment = hash < 0 ? "" : url[hash..];

        if (path.StartsWith("../", StringComparison.Ordinal))
        {
            external = true;
            return new Uri(RepositoryFolder, url).AbsoluteUri;
        }

        if (!image && ChapterFile().Match(path) is { Success: true } chapter)
        {
            return PageRoute(chapter.Groups[1].Value) + fragment;
        }

        if (!image && path == "README.md")
        {
            return PageRoute("") + fragment;
        }

        return ContentPath + url;
    }

    private static string PlainText(ContainerInline inline) => string.Concat(inline.Descendants<Inline>().Select(child => child switch
    {
        LiteralInline literal => literal.Content.ToString(),
        CodeInline code => code.Content,
        _ => "",
    }));

    [GeneratedRegex(@"^(\d\d[-_][a-z0-9_-]+)\.md$")]
    private static partial Regex ChapterFile();
}

public sealed record DocsChapter(string Slug, string Title)
{
    public string Number => Slug[..2];
}

public sealed record DocsPage(string Slug, string Title, string Html);

