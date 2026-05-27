using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Projects.Labels;
using AiIssueTracker.Api.Tests.Features.Projects;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Tests.Features.Projects.Labels;

[Collection(ProjectsTestsCollection.Name)]
public class LabelsTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User MakeUser(long id, string email, string name) =>
        new()
        {
            Id = id,
            Email = email,
            PasswordHash = "hashed",
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static Project MakeProject(long id, long ownerId, string slug) =>
        new()
        {
            Id = id,
            Slug = slug,
            Name = "Project",
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Label MakeLabel(long id, long projectId, string name, string color = "#aabbcc") =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            Name = name,
            Color = color,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static Issue MakeIssue(long id, long projectId, long reporterId) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = 1,
            Title = "Issue",
            Status = IssueStatus.Backlog,
            Priority = IssuePriority.Medium,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // =========================================================================
    // ListLabels
    // =========================================================================

    [Fact]
    public async Task ListLabels_WithLabels_Returns200SortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "list-proj");
        await fixture.Database.Save(user, project,
            MakeLabel(30L, 20L, "zebra"),
            MakeLabel(31L, 20L, "apple"),
            MakeLabel(32L, 20L, "mango"));

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/projects/list-proj/labels", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LabelDto[]>(ct);
        body.Should().NotBeNull();
        body!.Should().HaveCount(3);
        body[0].Name.Should().Be("apple");
        body[1].Name.Should().Be("mango");
        body[2].Name.Should().Be("zebra");
    }

    [Fact]
    public async Task ListLabels_NoLabels_Returns200EmptyArray()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "empty-proj");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/projects/empty-proj/labels", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LabelDto[]>(ct);
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    // =========================================================================
    // CreateLabel
    // =========================================================================

    [Fact]
    public async Task CreateLabel_ValidInput_Returns201WithNormalizedLowercaseName()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "create-proj");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/create-proj/labels",
            new { name = "BUG", color = "#ff0000" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<LabelDto>(ct);
        body.Should().NotBeNull();
        body!.Name.Should().Be("bug");
        body.Color.Should().Be("#ff0000");
    }

    [Fact]
    public async Task CreateLabel_DuplicateNameCaseInsensitive_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "dup-proj");
        await fixture.Database.Save(user, project, MakeLabel(30L, 20L, "bug"));

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/dup-proj/labels",
            new { name = "BUG", color = "#00ff00" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:label:name:already_exists");
    }

    [Fact]
    public async Task CreateLabel_InvalidHexColor_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "hex-proj");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/hex-proj/labels",
            new { name = "tag", color = "notacolor" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("common:validation:failed");
    }

    [Fact]
    public async Task CreateLabel_AtCapOf200_Returns400TooMany()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "cap-proj");
        await fixture.Database.Save(user, project);

        // Seed exactly 200 labels via Execute for efficiency
        await fixture.Database.Execute(async (AppDbContext db) =>
        {
            var labels = Enumerable.Range(1, 200).Select(i => new Label
            {
                Id = 1000L + i,
                ProjectId = 20L,
                Name = $"label{i:D3}",
                Color = "#aaaaaa",
                CreatedAt = DateTimeOffset.UtcNow,
            }).ToList();
            db.Labels.AddRange(labels);
            await db.SaveChangesAsync();
        });

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/cap-proj/labels",
            new { name = "overflow", color = "#123456" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:label:too_many");
    }

    // =========================================================================
    // UpdateLabel
    // =========================================================================

    [Fact]
    public async Task UpdateLabel_ByOwner_Returns200WithNewValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner   = MakeUser(10L, "owner@example.com", "Owner");
        var project = MakeProject(20L, 10L, "upd-lbl-proj");
        var label   = MakeLabel(30L, 20L, "old", "#111111");
        await fixture.Database.Save(owner, project, label);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PutAsJsonAsync(
            $"/api/projects/upd-lbl-proj/labels/{IdEncoding.Encode(30L)}",
            new { name = "New", color = "#999999" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LabelDto>(ct);
        body.Should().NotBeNull();
        body!.Name.Should().Be("new");
        body.Color.Should().Be("#999999");
    }

    [Fact]
    public async Task UpdateLabel_ByNonOwner_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner   = MakeUser(10L, "owner@example.com", "Owner");
        var other   = MakeUser(11L, "other@example.com", "Other");
        var project = MakeProject(20L, 10L, "403-lbl-proj");
        var label   = MakeLabel(30L, 20L, "tag");
        await fixture.Database.Save(owner, other, project, label);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(11L));
        var response = await client.PutAsJsonAsync(
            $"/api/projects/403-lbl-proj/labels/{IdEncoding.Encode(30L)}",
            new { name = "newtag", color = "#ffffff" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:label:edit:forbidden");
    }

    [Fact]
    public async Task UpdateLabel_RenameCollision_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner   = MakeUser(10L, "owner@example.com", "Owner");
        var project = MakeProject(20L, 10L, "coll-proj");
        var labelA  = MakeLabel(30L, 20L, "alpha");
        var labelB  = MakeLabel(31L, 20L, "beta");
        await fixture.Database.Save(owner, project, labelA, labelB);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PutAsJsonAsync(
            $"/api/projects/coll-proj/labels/{IdEncoding.Encode(30L)}",
            new { name = "beta", color = "#aabbcc" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:label:name:already_exists");
    }

    [Fact]
    public async Task UpdateLabel_UnknownLabel_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner   = MakeUser(10L, "owner@example.com", "Owner");
        var project = MakeProject(20L, 10L, "404-lbl-proj");
        await fixture.Database.Save(owner, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PutAsJsonAsync(
            $"/api/projects/404-lbl-proj/labels/{IdEncoding.Encode(99999L)}",
            new { name = "ghost", color = "#aabbcc" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:label:not_found");
    }

    // =========================================================================
    // DeleteLabel
    // =========================================================================

    [Fact]
    public async Task DeleteLabel_ByOwner_Returns204AndRemovesLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner   = MakeUser(10L, "owner@example.com", "Owner");
        var project = MakeProject(20L, 10L, "del-lbl-proj");
        var label   = MakeLabel(30L, 20L, "removeme");
        await fixture.Database.Save(owner, project, label);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.DeleteAsync(
            $"/api/projects/del-lbl-proj/labels/{IdEncoding.Encode(30L)}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await fixture.Database.SingleOrDefault<Label>(l => l.Id == 30L, ct);
        gone.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLabel_ByNonOwner_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner   = MakeUser(10L, "owner@example.com", "Owner");
        var other   = MakeUser(11L, "other@example.com", "Other");
        var project = MakeProject(20L, 10L, "del403-proj");
        var label   = MakeLabel(30L, 20L, "protected");
        await fixture.Database.Save(owner, other, project, label);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(11L));
        var response = await client.DeleteAsync(
            $"/api/projects/del403-proj/labels/{IdEncoding.Encode(30L)}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:label:edit:forbidden");
    }

    [Fact]
    public async Task DeleteLabel_CascadesIssueLabelLinks_ButLeavesIssueIntact()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner   = MakeUser(10L, "owner@example.com", "Owner");
        var project = MakeProject(20L, 10L, "casc-lbl-proj");
        var label   = MakeLabel(30L, 20L, "cascaded");
        var issue   = MakeIssue(40L, 20L, 10L);
        var link    = new IssueLabel { IssueId = 40L, LabelId = 30L };
        await fixture.Database.Save(owner, project, label, issue, link);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.DeleteAsync(
            $"/api/projects/casc-lbl-proj/labels/{IdEncoding.Encode(30L)}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // IssueLabel row cascaded
        var linkCount = await fixture.Database.Execute<int>(
            db => db.IssueLabels.CountAsync(il => il.LabelId == 30L, ct));
        linkCount.Should().Be(0);

        // Issue itself untouched
        var stillThere = await fixture.Database.SingleOrDefault<Issue>(i => i.Id == 40L, ct);
        stillThere.Should().NotBeNull();
    }

    // =========================================================================
    // Nested validator unit tests
    // =========================================================================

    public class ValidatorTests
    {
        private readonly CreateLabel.RequestValidator _createValidator = new();
        private readonly UpdateLabel.RequestValidator _updateValidator = new();

        // ---- CreateLabel validator ----

        [Fact]
        public void Create_Name_empty_fails_with_correct_error_code()
        {
            var result = _createValidator.TestValidate(new CreateLabel.Request("slug", "", "#aabbcc"));
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorCode("projects:label:name:required_or_too_long");
        }

        [Fact]
        public void Create_Name_41_chars_fails_with_correct_error_code()
        {
            var result = _createValidator.TestValidate(
                new CreateLabel.Request("slug", new string('a', 41), "#aabbcc"));
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorCode("projects:label:name:required_or_too_long");
        }

        [Fact]
        public void Create_Name_40_chars_passes()
        {
            var result = _createValidator.TestValidate(
                new CreateLabel.Request("slug", new string('a', 40), "#aabbcc"));
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Create_Color_missing_hash_fails_with_correct_error_code()
        {
            var result = _createValidator.TestValidate(new CreateLabel.Request("slug", "tag", "aabbcc"));
            result.ShouldHaveValidationErrorFor(x => x.Color)
                .WithErrorCode("projects:label:color:invalid");
        }

        [Fact]
        public void Create_Color_short_hex_fails_with_correct_error_code()
        {
            var result = _createValidator.TestValidate(new CreateLabel.Request("slug", "tag", "#abc"));
            result.ShouldHaveValidationErrorFor(x => x.Color)
                .WithErrorCode("projects:label:color:invalid");
        }

        [Fact]
        public void Create_Color_valid_6digit_hex_passes()
        {
            var result = _createValidator.TestValidate(new CreateLabel.Request("slug", "tag", "#aabb99"));
            result.ShouldNotHaveValidationErrorFor(x => x.Color);
        }

        // ---- UpdateLabel validator (same rules) ----

        [Fact]
        public void Update_Name_empty_fails_with_correct_error_code()
        {
            var result = _updateValidator.TestValidate(
                new UpdateLabel.Request("slug", "labelid", "", "#aabbcc"));
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorCode("projects:label:name:required_or_too_long");
        }

        [Fact]
        public void Update_Color_invalid_fails_with_correct_error_code()
        {
            var result = _updateValidator.TestValidate(
                new UpdateLabel.Request("slug", "labelid", "tag", "bad"));
            result.ShouldHaveValidationErrorFor(x => x.Color)
                .WithErrorCode("projects:label:color:invalid");
        }

        [Fact]
        public void Update_ValidInput_passes()
        {
            var result = _updateValidator.TestValidate(
                new UpdateLabel.Request("slug", "labelid", "valid-name", "#123abc"));
            result.IsValid.Should().BeTrue();
        }
    }
}
