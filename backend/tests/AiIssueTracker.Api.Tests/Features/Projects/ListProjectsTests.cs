using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Common.Pagination;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Projects;
using AwesomeAssertions;

namespace AiIssueTracker.Api.Tests.Features.Projects;

[Collection(ProjectsTestsCollection.Name)]
public class ListProjectsTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User MakeUser(long id, string email = "owner@example.com", string name = "Owner") =>
        new()
        {
            Id = id,
            Email = email,
            PasswordHash = "hashed",
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static Project MakeProject(long id, long ownerId, string slug, string name,
        DateTimeOffset? createdAt = null) =>
        new()
        {
            Id = id,
            Slug = slug,
            Name = name,
            OwnerId = ownerId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // Test 1: Pagination round-trip — 25 projects, pages of 10
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_paginate_correctly_across_three_pages()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1001L, "paginator@example.com", "Paginator");

        // Seed 25 projects with distinct timestamps so ordering is deterministic
        var baseTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var projects = Enumerable.Range(1, 25)
            .Select(i => MakeProject(
                id: 2000L + i,
                ownerId: 1001L,
                slug: $"project-{i:D2}",
                name: $"Project {i:D2}",
                createdAt: baseTime.AddSeconds(i)))
            .ToArray();

        await fixture.Database.Save(user);
        await fixture.Database.Save(projects);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1001L));

        // Page 1
        var page1Response = await client.GetAsync("/api/projects?maxPageSize=10", ct);
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Response.Content.ReadFromJsonAsync<PageResponse<ListProjects.ProjectSummary>>(ct);
        page1.Should().NotBeNull();
        page1!.Items.Should().HaveCount(10);
        page1.NextPageToken.Should().NotBeNull();

        // Page 2
        var page2Response = await client.GetAsync(
            $"/api/projects?maxPageSize=10&pageToken={Uri.EscapeDataString(page1.NextPageToken!)}", ct);
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Response.Content.ReadFromJsonAsync<PageResponse<ListProjects.ProjectSummary>>(ct);
        page2.Should().NotBeNull();
        page2!.Items.Should().HaveCount(10);
        page2.NextPageToken.Should().NotBeNull();

        // Page 3
        var page3Response = await client.GetAsync(
            $"/api/projects?maxPageSize=10&pageToken={Uri.EscapeDataString(page2.NextPageToken!)}", ct);
        page3Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page3 = await page3Response.Content.ReadFromJsonAsync<PageResponse<ListProjects.ProjectSummary>>(ct);
        page3.Should().NotBeNull();
        page3!.Items.Should().HaveCount(5);
        page3.NextPageToken.Should().BeNull();

        // No duplicate IDs across all three pages
        var allIds = page1.Items.Select(p => p.Id)
            .Concat(page2.Items.Select(p => p.Id))
            .Concat(page3.Items.Select(p => p.Id))
            .ToList();

        allIds.Should().HaveCount(25);
        allIds.Distinct().Should().HaveCount(25);
    }

    // -------------------------------------------------------------------------
    // Test 2: ?q= prefix filter — only projects whose name or slug starts with "foo"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_filter_by_q_prefix_on_name_and_slug()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1002L, "filterer@example.com", "Filterer");

        var baseTime = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);

        // foo-1 and foo-bar should match ?q=foo
        var fooOne = MakeProject(3001L, 1002L, "foo-1", "foo-1", baseTime.AddSeconds(1));
        var fooBar = MakeProject(3002L, 1002L, "foo-bar", "foo-bar", baseTime.AddSeconds(2));
        // bar-foo and xyz should NOT match
        var barFoo = MakeProject(3003L, 1002L, "bar-foo", "bar-foo", baseTime.AddSeconds(3));
        var xyz = MakeProject(3004L, 1002L, "xyz", "xyz", baseTime.AddSeconds(4));

        await fixture.Database.Save(user);
        await fixture.Database.Save(fooOne, fooBar, barFoo, xyz);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1002L));

        var response = await client.GetAsync("/api/projects?q=foo", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PageResponse<ListProjects.ProjectSummary>>(ct);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);

        var slugs = result.Items.Select(p => p.Slug).ToList();
        slugs.Should().Contain("foo-1");
        slugs.Should().Contain("foo-bar");
        slugs.Should().NotContain("bar-foo");
        slugs.Should().NotContain("xyz");
    }

    // -------------------------------------------------------------------------
    // Test 3: Stable sort — same CreatedAt, order by descending Id
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_sort_by_descending_id_when_created_at_is_equal()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1003L, "sorter@example.com", "Sorter");

        var sameTime = new DateTimeOffset(2024, 3, 15, 12, 0, 0, TimeSpan.Zero);

        // Three projects with identical CreatedAt but different IDs
        var projectA = MakeProject(4001L, 1003L, "sort-a", "Sort A", sameTime);
        var projectB = MakeProject(4002L, 1003L, "sort-b", "Sort B", sameTime);
        var projectC = MakeProject(4003L, 1003L, "sort-c", "Sort C", sameTime);

        await fixture.Database.Save(user);
        await fixture.Database.Save(projectA, projectB, projectC);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1003L));

        var response = await client.GetAsync("/api/projects", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PageResponse<ListProjects.ProjectSummary>>(ct);
        result.Should().NotBeNull();

        // Filter down to only the three sort-* projects in case there's other data
        var sortProjects = result!.Items
            .Where(p => p.Slug.StartsWith("sort-"))
            .ToList();

        sortProjects.Should().HaveCount(3);

        // Descending Id: sort-c (4003), sort-b (4002), sort-a (4001)
        sortProjects[0].Slug.Should().Be("sort-c");
        sortProjects[1].Slug.Should().Be("sort-b");
        sortProjects[2].Slug.Should().Be("sort-a");
    }

    // -------------------------------------------------------------------------
    // Test 4: Token reuse with changed ?q= → 400 with paging:validation:page_token_invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_400_when_page_token_replayed_with_different_q()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1004L, "replayer@example.com", "Replayer");

        var baseTime = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero);

        // Seed enough foo-* projects to get a nextPageToken
        var fooProjects = Enumerable.Range(1, 10)
            .Select(i => MakeProject(
                id: 5000L + i,
                ownerId: 1004L,
                slug: $"foo-replay-{i:D2}",
                name: $"Foo Replay {i:D2}",
                createdAt: baseTime.AddSeconds(i)))
            .ToArray();

        await fixture.Database.Save(user);
        await fixture.Database.Save(fooProjects);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1004L));

        // Get page 1 with ?q=foo&maxPageSize=5 to obtain a nextPageToken
        var page1Response = await client.GetAsync("/api/projects?q=foo-replay&maxPageSize=5", ct);
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Response.Content.ReadFromJsonAsync<PageResponse<ListProjects.ProjectSummary>>(ct);
        page1.Should().NotBeNull();
        page1!.NextPageToken.Should().NotBeNull();

        // Replay that token with a different q= value
        var replayUrl = $"/api/projects?q=bar&pageToken={Uri.EscapeDataString(page1.NextPageToken!)}&maxPageSize=5";
        var replayResponse = await client.GetAsync(replayUrl, ct);

        replayResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await replayResponse.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem.Should().NotBeNull();
        problem!.ErrorCode.Should().Be("paging:validation:page_token_invalid");
    }

    // -------------------------------------------------------------------------
    // Test 5: Invalid maxPageSize values → 400 with paging:validation:max_page_size_invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_400_when_max_page_size_is_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1005L, "invalid-size@example.com", "Invalid Size");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1005L));

        var response = await client.GetAsync("/api/projects?maxPageSize=0", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem.Should().NotBeNull();
        problem!.ErrorCode.Should().Be("paging:validation:max_page_size_invalid");
    }

    [Fact]
    public async Task Should_return_400_when_max_page_size_exceeds_maximum()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1006L, "too-large@example.com", "Too Large");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1006L));

        var response = await client.GetAsync("/api/projects?maxPageSize=101", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem.Should().NotBeNull();
        problem!.ErrorCode.Should().Be("paging:validation:max_page_size_invalid");
    }
}
