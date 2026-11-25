using System.ComponentModel.DataAnnotations;

namespace ShopApi.Filters;

/// <summary>
/// Minimal APIs don't validate DataAnnotations automatically (controllers do).
/// This endpoint filter validates the request body of type T and returns a 400 ValidationProblem.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = new[] { "Request body is required." }
            });
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : new[] { "" })
                    .Select(m => (Member: m, Message: r.ErrorMessage ?? "Invalid value.")))
                .GroupBy(x => x.Member)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
