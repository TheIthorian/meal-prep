using System.Linq.Expressions;
using Api.Endpoints.Responses;

namespace Api.Models.Filter;

/// <summary>
///     Provides helpers for paginating query results.
/// </summary>
public static class QueryablePaginationExtensions
{
    extension<T>(IQueryable<T> query)
    {
        public IQueryable<T> ApplyPagination(PaginationOptions options, string defaultOrderBy = "Id") {
            // Select field = fallback if not supplied
            var orderField = string.IsNullOrWhiteSpace(options.OrderBy)
                ? defaultOrderBy
                : options.OrderBy;

            // Ordering
            query = query.OrderByDynamic(orderField, options.Direction);

            // Paging
            var skip = (options.Page - 1) * options.PageSize;
            return query.Skip(skip).Take(options.PageSize);
        }

        private IQueryable<T> OrderByDynamic(string propertyName, PaginationOptions.OrderDirections direction) {
            var param = Expression.Parameter(typeof(T), "x");
            var prop = Expression.PropertyOrField(param, propertyName);
            var lambda = Expression.Lambda(prop, param);

            var method = direction == PaginationOptions.OrderDirections.Asc
                ? nameof(Queryable.OrderBy)
                : nameof(Queryable.OrderByDescending);

            var call = Expression.Call(
                typeof(Queryable),
                method,
                new[] { typeof(T), prop.Type },
                query.Expression,
                Expression.Quote(lambda)
            );

            return query.Provider.CreateQuery<T>(call);
        }
    }
}
