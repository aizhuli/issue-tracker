using System.Collections;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace AiIssueTracker.Api.Tests.Harnesses;

public class DatabaseHarness<TProgram, TDbContext>(string databaseResourceName) : IHarness<TProgram>
    where TProgram : class
    where TDbContext : DbContext
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<TProgram>? _factory;
    private bool _started;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.UseSetting(
            $"ConnectionStrings:{databaseResourceName}",
            _postgres!.GetConnectionString());
    }

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellationToken)
    {
        _factory = factory;

        _postgres = new PostgreSqlBuilder("postgres:16.4-alpine3.20")
            .Build();

        await _postgres.StartAsync(cancellationToken);
        _started = true;
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (_postgres is not null)
        {
            await _postgres.StopAsync(cancellationToken);
            await _postgres.DisposeAsync();
        }
        _started = false;
    }

    public async Task Save(params object[] entities)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        foreach (var collection in entities.OfType<IEnumerable>())
        {
            dbContext.AddRange(collection);
        }

        var singleEntities = entities.Where(e => e is not IEnumerable);
        dbContext.AddRange(singleEntities);

        await dbContext.SaveChangesAsync();
    }

    public async Task Execute(Func<TDbContext, Task> action)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(db);
    }

    public async Task<T> Execute<T>(Func<TDbContext, Task<T>> action)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await action(db);
    }

    public async Task<T?> SingleOrDefault<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken)
        where T : class
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await db.Set<T>().SingleOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task Clear(CancellationToken cancellationToken)
    {
        ThrowIfNotStarted();

        await using var connection = new NpgsqlConnection(_postgres!.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            SchemasToInclude = ["public"],
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
        });

        await respawner.ResetAsync(connection);
    }

    private void ThrowIfNotStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException(
                $"Database harness is not started. Call {nameof(Start)} first.");
        }
    }
}
