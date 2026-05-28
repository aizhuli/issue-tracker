using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AiIssueTracker.Api.Tests.Harnesses;

public sealed class FakeChatClient : IChatClient
{
    private const string DefaultJson =
        """{"priority":"medium","labels":[],"acceptanceCriteria":"Default acceptance criteria."}""";

    private string _nextJson = DefaultJson;
    private bool _throwOnNextCall;

    /// <summary>The messages passed to the most recent <see cref="GetResponseAsync"/> call.</summary>
    public IEnumerable<ChatMessage>? LastMessages { get; private set; }

    /// <summary>Total number of times <see cref="GetResponseAsync"/> was called, including throwing calls.</summary>
    public int CallCount { get; private set; }

    // ── Configuration helpers ────────────────────────────────────────────────

    /// <summary>Sets the next response text. The handler receives this string as-is and parses it as JSON.</summary>
    public void RespondWithJson(string json) => _nextJson = json;

    /// <summary>Alias for <see cref="RespondWithJson"/>. Sets the next response text verbatim.</summary>
    public void RespondWithRaw(string text) => _nextJson = text;

    public void ThrowOnNextCall() => _throwOnNextCall = true;

    /// <summary>Resets all state back to defaults so each test starts clean.</summary>
    public void Reset()
    {
        _nextJson = DefaultJson;
        _throwOnNextCall = false;
        LastMessages = null;
        CallCount = 0;
    }

    // ── IChatClient ──────────────────────────────────────────────────────────

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastMessages = messages.ToList();

        if (_throwOnNextCall)
        {
            _throwOnNextCall = false;
            throw new HttpRequestException("Simulated LLM unavailability.");
        }

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _nextJson));
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var msg in response.Messages)
            yield return new ChatResponseUpdate(msg.Role, msg.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
