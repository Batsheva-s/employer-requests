using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Application.Services;
using EmployerRequests.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployerRequests.UnitTests.Services;

public class RequestStatusServiceTests
{
    private static readonly byte[] Version1 = [0, 0, 0, 0, 0, 0, 0, 1];
    private static readonly byte[] Version2 = [0, 0, 0, 0, 0, 0, 0, 2];

    [Fact]
    public async Task A_change_based_on_an_outdated_version_is_a_conflict_and_nothing_is_saved()
    {
        var repository = new FakeRequestRepository();
        var request = repository.Add(TestRequests.Create(1, RequestStatus.New, Version2));
        var service = CreateService(repository, new CountingSummaryCache());

        // The user saw version 1, but the request is already in version 2.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.ChangeStatusAsync(
            1, new ChangeStatusCommand { Status = RequestStatus.InProgress, RowVersion = Version1 }, CancellationToken.None));

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
        var cache = new CountingSummaryCache();
        var service = CreateService(repository, cache);

        var result = await service.ChangeStatusBulkAsync(new BulkChangeStatusCommand
        {
            Status = RequestStatus.InProgress,
            Items =
            [
                Item(1, Version1),      // can be updated
                Item(2, Version1),      // the user saw version 1; it is in version 2 now
                Item(3, Version1),      // already in progress: InProgress → InProgress is not a move
                Item(404, Version1)     // does not exist
            ]
        }, CancellationToken.None);

        Assert.Equal(
            new[] { BulkItemOutcome.Updated, BulkItemOutcome.Conflict, BulkItemOutcome.InvalidTransition, BulkItemOutcome.NotFound },
            result.Items.Select(item => item.Outcome));
        Assert.Equal(4, result.Requested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(3, result.Failed);

        Assert.Equal(RequestStatus.InProgress, updatable.Status);
        Assert.Equal(RequestStatus.New, changedMeanwhile.Status);
        Assert.Equal(1, cache.Invalidations);
    }

    [Fact]
    public async Task A_conflict_found_by_the_database_while_saving_fails_only_that_request()
    {
        // Request 2 matched the user's version when it was loaded, but another user saved it before us –
        // only the database can see that. The other requests are still saved.
        var repository = new FakeRequestRepository { ConflictOnSaveForId = 2 };
        repository.Add(TestRequests.Create(1, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(2, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(3, RequestStatus.New, Version1));
        var service = CreateService(repository, new CountingSummaryCache());

        var result = await service.ChangeStatusBulkAsync(new BulkChangeStatusCommand
        {
            Status = RequestStatus.Waiting,
            Items = [Item(1, Version1), Item(2, Version1), Item(3, Version1)]
        }, CancellationToken.None);

        Assert.Equal(
            new[] { BulkItemOutcome.Updated, BulkItemOutcome.Conflict, BulkItemOutcome.Updated },
            result.Items.Select(item => item.Outcome));
        Assert.Equal(2, repository.SuccessfulSaves);
    }

    [Fact]
    public async Task An_unexpected_failure_in_the_middle_of_a_bulk_change_leaves_no_outdated_summary()
    {
        // Request 1 is saved; then the database fails on request 2. The error reaches the client as a 500,
        // but the change to request 1 is already saved – so the cached summary must already be cleared.
        var repository = new FakeRequestRepository { FailOnSaveForId = 2 };
        repository.Add(TestRequests.Create(1, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(2, RequestStatus.New, Version1));
        repository.Add(TestRequests.Create(3, RequestStatus.New, Version1));
        var cache = new CountingSummaryCache();
        var service = CreateService(repository, cache);

        await Assert.ThrowsAsync<TimeoutException>(() => service.ChangeStatusBulkAsync(new BulkChangeStatusCommand
        {
            Status = RequestStatus.Waiting,
            Items = [Item(1, Version1), Item(2, Version1), Item(3, Version1)]
        }, CancellationToken.None));

        Assert.Equal(1, repository.SuccessfulSaves);
        Assert.Equal(1, cache.Invalidations);
    }

    private static RequestStatusService CreateService(FakeRequestRepository repository, CountingSummaryCache cache) =>
        new(repository, cache, new FixedUser(), TimeProvider.System, NullLogger<RequestStatusService>.Instance);

    private static BulkChangeStatusItem Item(int id, byte[] rowVersion) => new() { Id = id, RowVersion = rowVersion };
}
