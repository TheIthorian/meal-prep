using Microsoft.Extensions.Primitives;

namespace Api.Endpoints.Responses;

/// <summary>
///     A pagination wrapper for a set of results
/// </summary>
public record PaginatedResponse<T>(
    IEnumerable<T> Data,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
)
{
    public static PaginatedResponse<T> ToPaginatedResponse(
        IEnumerable<T> data,
        PaginationOptions paginationOptions,
        int totalCount
    ) {
        return new PaginatedResponse<T>(
            data,
            paginationOptions.Page,
            paginationOptions.PageSize,
            totalCount,
            paginationOptions.GetTotalPages(totalCount)
        );
    }
}

public record PaginationOptions(
    int Page,
    int PageSize,
    string? OrderBy,
    PaginationOptions.OrderDirections Direction,
    IEnumerable<KeyValuePair<string, StringValues>> FilterOptions
)
{
    public enum OrderDirections
    {
        Asc,
        Desc
    }

    public static readonly int MaxPageLength = 100;

    public static PaginationOptions FromQueryParams(IQueryCollection query) {
        // Parse page with default value
        var parsedPage = 1;
        if (query.TryGetValue("page", out var pageValue) && int.TryParse(pageValue, out var page))
            parsedPage = Math.Max(1, page);

        // Parse pageSize with default value
        var parsedPageSize = 20;
        if (query.TryGetValue("pageSize", out var pageSizeValue) && int.TryParse(pageSizeValue, out var pageSize))
            parsedPageSize = Math.Max(1, Math.Min(pageSize, MaxPageLength));

        // Parse orderBy
        string? orderBy = null;
        if (query.TryGetValue("orderBy", out var orderByValue)) orderBy = orderByValue.ToString();

        // Parse orderDirection with default to Desc
        var direction = OrderDirections.Desc;
        if (query.TryGetValue("direction", out var directionValue))
            direction = directionValue.ToString().Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? OrderDirections.Asc
                : OrderDirections.Desc;

        return new PaginationOptions(parsedPage, parsedPageSize, orderBy, direction, query);
    }

    public int GetTotalPages(int totalCount) {
        return (int)Math.Ceiling((double)totalCount / PageSize);
    }
}
