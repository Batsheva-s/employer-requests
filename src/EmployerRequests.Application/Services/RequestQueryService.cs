using EmployerRequests.Application.Abstractions;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Exceptions;

namespace EmployerRequests.Application.Services;

public class RequestQueryService : IRequestQueryService
{
    private readonly IRequestRepository _repository;

    public RequestQueryService(IRequestRepository repository)
    {
        _repository = repository;
    }

    public Task<PagedResult<RequestListItemDto>> SearchAsync(RequestSearchQuery query, CancellationToken cancellationToken) =>
        _repository.SearchAsync(query, cancellationToken);

    public async Task<RequestDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var request = await _repository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("Request", id);

        return request.ToDetailsDto();
    }

    public async Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int id, CancellationToken cancellationToken)
    {
        // An empty history is a valid answer for an existing request, so existence is checked separately.
        if (!await _repository.ExistsAsync(id, cancellationToken))
        {
            throw new NotFoundException("Request", id);
        }

        return await _repository.GetHistoryAsync(id, cancellationToken);
    }
}
