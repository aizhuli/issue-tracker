using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiIssueTracker.Api.Tests.Harnesses;

public class ChatClientHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    public FakeChatClient Fake { get; } = new();

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(Fake);
        });
    }

    public Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task Stop(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
