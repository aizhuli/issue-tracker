using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

namespace AiIssueTracker.Api.Integrations.Llm;

public static class LlmRegistration
{
    public static IHostApplicationBuilder AddLlmChatClient(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return new OpenAIClient(
                    new ApiKeyCredential(opts.ApiKey ?? "noop"),
                    new OpenAIClientOptions { Endpoint = new Uri(opts.BaseUrl!) })
                .GetChatClient(opts.Model)
                .AsIChatClient()
                .AsBuilder()
                .UseLogging(loggerFactory)
                .Build();
        });

        return builder;
    }
}
