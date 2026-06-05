using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiIssueTracker.Api.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;

namespace AiIssueTracker.Api.Integrations.GitHub;

public class GitHubClient(HttpClient http)
{
    private static readonly Regex PrUrlRegex =
        new(@"^https://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/(?<number>\d+)$",
            RegexOptions.Compiled);

    public static (string owner, string repo, int number) ParsePrUrl(string url)
    {
        var match = PrUrlRegex.Match(url);
        if (!match.Success)
            throw new ValidationException(new[]
            {
                new ValidationFailure("PullRequestUrl", "Invalid GitHub PR URL.")
                {
                    ErrorCode = "ai:pr_review:pull_request_url:invalid"
                }
            });
        return (match.Groups["owner"].Value, match.Groups["repo"].Value,
            int.Parse(match.Groups["number"].Value));
    }

    public async Task<string> FetchMetadataAsync(string url, CancellationToken ct = default)
    {
        var (owner, repo, number) = ParsePrUrl(url);
        using var doc = await GetJsonDocumentAsync($"/repos/{owner}/{repo}/pulls/{number}", ct);
        var root = doc.RootElement;
        var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "";
        var body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
        var author = root.TryGetProperty("user", out var userEl) &&
                     userEl.TryGetProperty("login", out var loginEl)
            ? loginEl.GetString() ?? ""
            : "";
        var baseRef = root.TryGetProperty("base", out var baseEl) &&
                      baseEl.TryGetProperty("ref", out var baseRefEl)
            ? baseRefEl.GetString() ?? ""
            : "";
        var headRef = root.TryGetProperty("head", out var headEl) &&
                      headEl.TryGetProperty("ref", out var headRefEl)
            ? headRefEl.GetString() ?? ""
            : "";
        return $"Title: {title}\nAuthor: {author}\nBase: {baseRef} ← Head: {headRef}\n\n{body}";
    }

    public async Task<string> FetchDiffAsync(string url, CancellationToken ct = default)
    {
        var (owner, repo, number) = ParsePrUrl(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/repos/{owner}/{repo}/pulls/{number}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));
        using var response = await SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        const int limit = 50 * 1024;
        if (content.Length > limit)
            content = content[..limit] + "\n[diff truncated]";
        return content;
    }

    public async Task<string> FetchChangedFilesAsync(string url, CancellationToken ct = default)
    {
        var (owner, repo, number) = ParsePrUrl(url);
        using var doc = await GetJsonDocumentAsync($"/repos/{owner}/{repo}/pulls/{number}/files", ct);
        var lines = doc.RootElement.EnumerateArray()
            .Select(f =>
            {
                var filename = f.TryGetProperty("filename", out var filenameEl) ? filenameEl.GetString() ?? "" : "";
                var status = f.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "" : "";
                return $"{status}: {filename}";
            });
        return string.Join("\n", lines);
    }

    public async Task<string> FetchFileContentAsync(string url, string path, CancellationToken ct = default)
    {
        var (owner, repo, number) = ParsePrUrl(url);
        // Get head SHA from PR metadata
        using var prDoc = await GetJsonDocumentAsync($"/repos/{owner}/{repo}/pulls/{number}", ct);
        var headSha = prDoc.RootElement.TryGetProperty("head", out var headEl) &&
                      headEl.TryGetProperty("sha", out var shaEl)
            ? shaEl.GetString() ?? ""
            : "";

        using var contentDoc = await GetJsonDocumentAsync(
            $"/repos/{owner}/{repo}/contents/{path.TrimStart('/')}?ref={headSha}", ct);
        var base64 = contentDoc.RootElement.TryGetProperty("content", out var contentEl)
            ? contentEl.GetString() ?? ""
            : "";
        // GitHub returns base64 with newlines — strip them
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Replace("\n", "")));
        const int limit = 20 * 1024;
        if (decoded.Length > limit)
            decoded = decoded[..limit] + "\n[content truncated]";
        return decoded;
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(string path, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(path, ct);
            await EnsureSuccessAsync(response);
            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BadGatewayException("GitHub is unreachable.", "ai:pr_review:github:fetch_failed");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BadGatewayException("GitHub is unreachable.", "ai:pr_review:github:fetch_failed");
        }
        await EnsureSuccessAsync(response);
        return response;
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new BadGatewayException("GitHub API error.", "ai:pr_review:github:fetch_failed");
        return Task.CompletedTask;
    }
}
