using FluentValidation;

namespace Api.Endpoints.Middleware;

/// <summary>
///     Provides endpoint filters for request validation.
/// </summary>
public static class ValidationMiddleware
{
    extension(RouteHandlerBuilder builder)
    {
        public RouteHandlerBuilder WithBodyValidation<BodyType>() {
            return builder.AddEndpointFilter(async (context, next) => {
                    // Try to get the body (bound model)
                    if (context.Arguments.FirstOrDefault(a => a is BodyType) is BodyType body) {
                        var validator = context.HttpContext.RequestServices.GetService<IValidator<BodyType>>();
                        if (validator == null) return Results.InternalServerError();

                        var result = await validator.ValidateAsync(body);
                        if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());
                    }

                    return await next(context);
                }
            );
        }
    }
}
