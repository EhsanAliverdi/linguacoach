using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>Phase K3 — archive/unarchive (soft-delete) for Resource Bank rows.</summary>
public sealed class ResourceBankArchiveHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceBankArchiveHandler _sut;
    private readonly ResourceBankQueryService _query;

    public ResourceBankArchiveHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ResourceBankArchiveHandler(_db);
        _query = new ResourceBankQueryService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private ResourceBankItem SeedItem(string word)
    {
        var source = new CefrResourceSource($"Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        _db.CefrResourceSources.Add(source);
        var item = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "A1",
            ResourceBankItemContent.Serialize(new VocabularyContent(word, null, null)));
        _db.ResourceBankItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    [Fact]
    public async Task Archived_item_is_excluded_from_the_default_unified_list()
    {
        var item = SeedItem("hello");

        var before = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());
        before.Items.Should().Contain(i => i.Id == item.Id);

        var archiveResult = await _sut.ArchiveAsync(new ArchiveResourceBankItemsCommand(new[] { item.Id }));
        archiveResult.SucceededCount.Should().Be(1);

        var after = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());
        after.Items.Should().NotContain(i => i.Id == item.Id);
    }

    [Fact]
    public async Task GetById_still_returns_an_archived_item_with_IsArchived_true()
    {
        var item = SeedItem("hello");
        await _sut.ArchiveAsync(new ArchiveResourceBankItemsCommand(new[] { item.Id }));

        var dto = await _query.GetUnifiedByIdAsync(item.Id);

        dto.Should().NotBeNull();
        dto!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task Unarchive_restores_visibility_in_the_default_list()
    {
        var item = SeedItem("hello");
        await _sut.ArchiveAsync(new ArchiveResourceBankItemsCommand(new[] { item.Id }));

        var unarchiveResult = await _sut.UnarchiveAsync(new UnarchiveResourceBankItemsCommand(new[] { item.Id }));
        unarchiveResult.SucceededCount.Should().Be(1);

        var after = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());
        after.Items.Should().Contain(i => i.Id == item.Id && !i.IsArchived);
    }

    [Fact]
    public async Task Archiving_a_nonexistent_id_is_reported_as_a_per_item_failure_not_an_exception()
    {
        var missingId = Guid.NewGuid();

        var result = await _sut.ArchiveAsync(new ArchiveResourceBankItemsCommand(new[] { missingId }));

        result.SucceededCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Id == missingId && !i.Success);
    }

    [Fact]
    public async Task Batch_archive_continues_on_error_for_a_mix_of_valid_and_invalid_ids()
    {
        var item = SeedItem("hello");
        var missingId = Guid.NewGuid();

        var result = await _sut.ArchiveAsync(new ArchiveResourceBankItemsCommand(new[] { item.Id, missingId }));

        result.RequestedCount.Should().Be(2);
        result.SucceededCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
    }
}
