using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AiIssueTracker.Api.Integrations.Llm;

public static class LlmRegistration
{
    public static IHostApplicationBuilder AddLlmChatClient(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var client = string.IsNullOrEmpty(opts.ApiKey)
                ? new AnthropicClient()
                : new AnthropicClient { ApiKey = opts.ApiKey };

            return client
                .AsIChatClient(opts.Model ?? "claude-opus-4-7")
                .AsBuilder()
                .UseLogging(loggerFactory)
                .Build();
        });

        return builder;
    }
}
