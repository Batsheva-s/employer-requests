using System.ComponentModel.DataAnnotations;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;

namespace EmployerRequests.UnitTests.Contracts;

/// <summary>
/// The validation rules of the contracts, checked the way ASP.NET Core checks them before an action runs.
/// The integration tests check that a broken rule really answers 400.
/// </summary>
public class ValidationTests
{
    private static readonly byte[] AnyVersion = [0, 0, 0, 0, 0, 0, 7, 209];

    [Fact]
    public void The_default_search_query_is_valid()
    {
        Assert.Empty(Validate(new RequestSearchQuery()));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Page_and_page_size_outside_the_limits_are_rejected(int page, int pageSize)
    {
        Assert.NotEmpty(Validate(new RequestSearchQuery { Page = page, PageSize = pageSize }));
    }

    [Fact]
    public void An_unknown_status_value_is_rejected()
    {
        var errors = Validate(new RequestSearchQuery { Status = [RequestStatus.New, (RequestStatus)99] });

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(RequestFilter.Status)));
    }

    [Fact]
    public void An_unknown_sort_field_is_rejected()
    {
        Assert.NotEmpty(Validate(new RequestSearchQuery { SortBy = (RequestSortField)42 }));
    }

    [Fact]
    public void A_date_range_that_does_not_end_after_it_starts_is_rejected()
    {
        var day = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.NotEmpty(Validate(new RequestSearchQuery { CreatedFrom = day, CreatedTo = day.AddDays(-1) }));
        Assert.NotEmpty(Validate(new RequestSearchQuery { CreatedFrom = day, CreatedTo = day }));
        Assert.Empty(Validate(new RequestSearchQuery { CreatedFrom = day, CreatedTo = day.AddDays(1) }));
    }

    [Fact]
    public void A_bulk_change_of_more_than_100_requests_is_rejected()
    {
        var command = new BulkChangeStatusCommand
        {
            Status = RequestStatus.InProgress,
            Items = Enumerable.Range(1, 101).Select(id => new BulkChangeStatusItem { Id = id, RowVersion = AnyVersion }).ToList()
        };

        Assert.NotEmpty(Validate(command));
    }

    [Fact]
    public void A_bulk_change_that_lists_a_request_twice_is_rejected()
    {
        var command = new BulkChangeStatusCommand
        {
            Status = RequestStatus.InProgress,
            Items =
            [
                new BulkChangeStatusItem { Id = 7, RowVersion = AnyVersion },
                new BulkChangeStatusItem { Id = 7, RowVersion = AnyVersion }
            ]
        };

        Assert.NotEmpty(Validate(command));
    }

    [Fact]
    public void A_version_that_is_not_8_bytes_is_rejected()
    {
        var command = new ChangeStatusCommand { Status = RequestStatus.InProgress, RowVersion = [1, 2, 3] };

        Assert.NotEmpty(Validate(command));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
