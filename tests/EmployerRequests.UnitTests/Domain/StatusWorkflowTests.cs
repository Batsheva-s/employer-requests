using EmployerRequests.Domain;

namespace EmployerRequests.UnitTests.Domain;

public class StatusWorkflowTests
{
    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.InProgress)]
    [InlineData(RequestStatus.New, RequestStatus.Waiting)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Waiting)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Completed)]
    [InlineData(RequestStatus.Waiting, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Completed)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress)]
    public void Moves_defined_in_the_workflow_are_allowed(RequestStatus from, RequestStatus to)
    {
        Assert.True(StatusWorkflow.CanMove(from, to));
    }

    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.Completed)]
    [InlineData(RequestStatus.InProgress, RequestStatus.New)]
    [InlineData(RequestStatus.Waiting, RequestStatus.New)]
    [InlineData(RequestStatus.Completed, RequestStatus.New)]
    [InlineData(RequestStatus.Completed, RequestStatus.Waiting)]
    [InlineData(RequestStatus.New, RequestStatus.New)]
    [InlineData(RequestStatus.Completed, RequestStatus.Completed)]
    public void Any_other_move_is_rejected(RequestStatus from, RequestStatus to)
    {
        Assert.False(StatusWorkflow.CanMove(from, to));
    }
}
