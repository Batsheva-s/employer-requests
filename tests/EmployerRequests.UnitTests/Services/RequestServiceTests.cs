using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Application.Services;
using EmployerRequests.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployerRequests.UnitTests.Services;

public class RequestServiceTests
{
    private const string User = "test-user";
    private static readonly byte[] Version1 = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };
    private static readonly byte[] Version2 = new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 };

    private readonly SummaryCache _summaryCache = new SummaryCache(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task A_change_based_on_an_outdated_version_is_a_conflict_and_nothing_is_saved()
    {
        var repository = new FakeRequestRepository();
        var request = repository.Add(TestRequests.Create(1, RequestStatus.New, Version2));
        var service = CreateService(repository);

        // The user saw version 1, but the request is already in version 2.
        var change = new ChangeStatusDto { Status = RequestStatus.InProgress, RowVersion = Version1 };
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => service.ChangeStatusAsync(1, change, User, CancellationToken.None));

        Assert.Equal(RequestStatus.New, request.Status);
        Assert.Equal(0, repository.SuccessfulSaves);
    }

    [Fact]
    public async Task Every_request_in_a_bulk_change_gets_its_own_outcome()
    {
        var repository = new FakeRequestRepository();
        var updatable = repository.Add(TestRequests.Create(1, RequestStatus.New, Version1));
        var changedMeanwhile = repository.Add(TestRequests.Create(2, RequestStatus.New, Version2));
        repository.Add(TestRequests.Create(3, RequestStatus.InProgress, Version1));
        var service = CreateService(repository);

        var bulkChange = new BulkChangeStatusDto
        {
            Status = RequestStatus.InProgress,
            Items = new List<BulkChangeStatusItemDto>
            {
                Item(1, Version1),      // can be updated
                Item(2, Version1),      // the user saw version 1; it is in version 2 now
                Item(3, Version1),      // already in progress: InProgress → InProgress is not a move
                Item(404, Version1)     // does not exist
            }
        };
        var result = await service.ChangeStatusBulkAsync(bulkChange, User, CancellationToken.None);

        Assert.Equal(
            new[] { BulkItemOutcome.Updated, BulkItemOutcome.Conflict, BulkItemOutcome.InvalidTransition, BulkItemOutcome.NotFound },
            result.Items.Select(item => item.Outcome));
        Assert.Equal(4, result.Requested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(3, result.Failed);

        Assert.Equal(RequestStatus.InProgress, updatable.Status);
        Assert.Equal(RequestStatus.New, changedMeanwhile.Status);
        var historyRow = Assert.Single(updatable.StatusChanges);
        Assert.Equal(User, historyRow.ChangedBy);
    }

    [Fact]
    public async Task A_conflict_found_by_the_database_while_saving_fails_only_that_request()
    {
        // Request 2 matched the user's version when it was loaded, but another user saved it before us –
        // only the database can see that. The other requests are still saved.
        var repository = new FakeRequestRepository();
        repository.ConflictOnSaveForId = 2;
        repository.Add(TestRequests.Create(1, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(2, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(3, RequestStatus.New, Version1));
        var service = CreateService(repository);

        var bulkChange = new BulkChangeStatusDto
        {
            Status = RequestStatus.Waiting,
            Items = new List<BulkChangeStatusItemDto> { Item(1, Version1), Item(2, Version1), Item(3, Version1) }
        };
        var result = await service.ChangeStatusBulkAsync(bulkChange, User, CancellationToken.None);

        Assert.Equal(
            new[] { BulkItemOutcome.Updated, BulkItemOutcome.Conflict, BulkItemOutcome.Updated },
            result.Items.Select(item => item.Outcome));
        Assert.Equal(2, repository.SuccessfulSaves);
    }

    [Fact]
    public async Task A_saved_change_clears_the_cached_summary()
    {
        var repository = new FakeRequestRepository();
        repository.Add(TestRequests.Create(1, RequestStatus.New, Version1));
        var service = CreateService(repository);
        FillSummaryCache();

        var change = new ChangeStatusDto { Status = RequestStatus.InProgress, RowVersion = Version1 };
        await service.ChangeStatusAsync(1, change, User, CancellationToken.None);

        Assert.Null(_summaryCache.Get());
    }

    [Fact]
    public async Task An_unexpected_failure_in_the_middle_of_a_bulk_change_leaves_no_outdated_summary()
    {
        // Request 1 is saved; then the database fails on request 2. The error reaches the client as a 500,
        // but the change to request 1 is already saved – so the cached summary must already be cleared.
        var repository = new FakeRequestRepository();
        repository.FailOnSaveForId = 2;
        repository.Add(TestRequests.Create(1, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(2, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(3, RequestStatus.New, Version1));
        var service = CreateService(repository);
        FillSummaryCache();

        var bulkChange = new BulkChangeStatusDto
        {
            Status = RequestStatus.Waiting,
            Items = new List<BulkChangeStatusItemDto> { Item(1, Version1), Item(2, Version1), Item(3, Version1) }
        };
        await Assert.ThrowsAsync<TimeoutException>(
            () => service.ChangeStatusBulkAsync(bulkChange, User, CancellationToken.None));

        Assert.Equal(1, repository.SuccessfulSaves);
        Assert.Null(_summaryCache.Get());
    }

    private RequestService CreateService(FakeRequestRepository repository)
    {
        return new RequestService(repository, _summaryCache, NullLogger<RequestService>.Instance);
    }

    private static BulkChangeStatusItemDto Item(int id, byte[] rowVersion)
    {
        return new BulkChangeStatusItemDto { Id = id, RowVersion = rowVersion };
    }

    /// <summary>Puts a summary in the cache, as the first read of the unfiltered summary does.</summary>
    private void FillSummaryCache()
    {
        var emptySummary = new RequestsSummaryDto { TotalCount = 0, CalculatedAt = DateTimeOffset.UtcNow };

        _summaryCache.Set(emptySummary, _summaryCache.GetVersion());
        Assert.NotNull(_summaryCache.Get());
    }
}
