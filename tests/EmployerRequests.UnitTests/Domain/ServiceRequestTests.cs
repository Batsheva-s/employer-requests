using EmployerRequests.Domain;

namespace EmployerRequests.UnitTests.Domain;

public class ServiceRequestTests
{
    private static readonly DateTimeOffset CreatedAt = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ChangedAt = new DateTimeOffset(2026, 1, 2, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ChangeStatus_moves_the_request_updates_the_time_and_records_the_change()
    {
        var request = new ServiceRequest("בירור תשלום", "אלון תעשיות בע\"מ", RequestPriority.High, "שרה כהן", CreatedAt);

        request.ChangeStatus(RequestStatus.InProgress, "dana", ChangedAt);

        Assert.Equal(RequestStatus.InProgress, request.Status);
        Assert.Equal(ChangedAt, request.UpdatedAt);

        var change = Assert.Single(request.StatusChanges);
        Assert.Equal(RequestStatus.New, change.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, change.NewStatus);
        Assert.Equal("dana", change.ChangedBy);
        Assert.Equal(ChangedAt, change.ChangedAt);
    }

    [Fact]
    public void ChangeStatus_to_a_status_the_workflow_does_not_allow_throws_and_changes_nothing()
    {
        var request = new ServiceRequest("בירור תשלום", "אלון תעשיות בע\"מ", RequestPriority.High, "שרה כהן", CreatedAt);

        var exception = Assert.Throws<InvalidStatusTransitionException>(
            () => request.ChangeStatus(RequestStatus.Completed, "dana", ChangedAt));

        Assert.Equal(RequestStatus.New, exception.CurrentStatus);
        Assert.Equal(RequestStatus.Completed, exception.RequestedStatus);
        Assert.Equal(new[] { RequestStatus.InProgress, RequestStatus.Waiting }, exception.AllowedStatuses);

        Assert.Equal(RequestStatus.New, request.Status);
        Assert.Equal(CreatedAt, request.UpdatedAt);
        Assert.Empty(request.StatusChanges);
    }
}
