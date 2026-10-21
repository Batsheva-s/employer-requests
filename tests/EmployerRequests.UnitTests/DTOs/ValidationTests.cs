using System.ComponentModel.DataAnnotations;
using EmployerRequests.Application.DTOs;
using EmployerRequests.Domain;

namespace EmployerRequests.UnitTests.DTOs;

/// <summary>
/// The validation rules of the DTOs, checked with Validator: the attributes first, then Validate() – the same order
/// as ASP.NET Core. (Validator does not go into nested objects, such as each item of a bulk change; ASP.NET Core does.)
/// The integration tests check that a broken rule really answers 400.
/// </summary>
public class ValidationTests
{
    private static readonly byte[] AnyVersion = new byte[] { 0, 0, 0, 0, 0, 0, 7, 209 };

    [Fact]
    public void The_default_search_is_valid()
    {
        Assert.Empty(Validate(new RequestSearchDto()));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Page_and_page_size_outside_the_limits_are_rejected(int page, int pageSize)
    {
        Assert.NotEmpty(Validate(new RequestSearchDto { Page = page, PageSize = pageSize }));
    }

    [Fact]
    public void An_unknown_status_value_is_rejected()
    {
        // 99 is not one of the enum's values. (Through HTTP, ?status=99 already fails model binding, and the answer is 400.)
        var errors = Validate(new RequestSearchDto { Status = new[] { RequestStatus.New, (RequestStatus)99 } });

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(RequestFilterDto.Status)));
    }

    [Fact]
    public void An_unknown_sort_field_is_rejected()
    {
        Assert.NotEmpty(Validate(new RequestSearchDto { SortBy = (RequestSortField)42 }));
    }

    [Fact]
    public void A_date_range_that_does_not_end_after_it_starts_is_rejected()
    {
        var day = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.NotEmpty(Validate(new RequestSearchDto { CreatedFrom = day, CreatedTo = day.AddDays(-1) }));
        Assert.NotEmpty(Validate(new RequestSearchDto { CreatedFrom = day, CreatedTo = day }));
        Assert.Empty(Validate(new RequestSearchDto { CreatedFrom = day, CreatedTo = day.AddDays(1) }));
    }

    [Fact]
    public void A_bulk_change_of_more_than_100_requests_is_rejected()
    {
        var items = new List<BulkChangeStatusItemDto>();
        for (var id = 1; id <= 101; id++)
        {
            items.Add(new BulkChangeStatusItemDto { Id = id, RowVersion = AnyVersion });
        }

        var bulkChange = new BulkChangeStatusDto { Status = RequestStatus.InProgress, Items = items };

        Assert.NotEmpty(Validate(bulkChange));
    }

    [Fact]
    public void A_bulk_change_that_lists_a_request_twice_is_rejected()
    {
        var bulkChange = new BulkChangeStatusDto
        {
            Status = RequestStatus.InProgress,
            Items = new List<BulkChangeStatusItemDto>
            {
                new BulkChangeStatusItemDto { Id = 7, RowVersion = AnyVersion },
                new BulkChangeStatusItemDto { Id = 7, RowVersion = AnyVersion }
            }
        };

        Assert.NotEmpty(Validate(bulkChange));
    }

    [Fact]
    public void A_version_that_is_not_8_bytes_is_rejected()
    {
        var change = new ChangeStatusDto { Status = RequestStatus.InProgress, RowVersion = new byte[] { 1, 2, 3 } };

        Assert.NotEmpty(Validate(change));
    }

    /// <summary>Runs the attributes and then Validate(), as ASP.NET Core does, and returns the errors.</summary>
    private static List<ValidationResult> Validate(object model)
    {
        var errors = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, errors, validateAllProperties: true);
        return errors;
    }
}
