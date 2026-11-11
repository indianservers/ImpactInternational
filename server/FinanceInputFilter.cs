using System.Reflection;

namespace Clubs;

public class FinanceInputFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var nullability = new NullabilityInfoContext();
        void Validate(object? value, int depth)
        {
            if (value == null)
                return;
            if (depth > 8)
                throw new ApiError(400, "Input nesting exceeds the limit.");
            var type = value.GetType();
            if (type == typeof(string))
            {
                if (((string)value).Length > 50000)
                    throw new ApiError(400, "Input text is too long.");
                return;
            }
            if (value is System.Collections.IEnumerable list && value is not string)
            {
                int count = 0;
                foreach (var item in list)
                {
                    if (++count > 1000)
                        throw new ApiError(400, "Input collection exceeds the limit.");
                    Validate(item, depth + 1);
                }
                return;
            }
            if (
                type.Namespace != "Clubs"
                || !(
                    type.Name.StartsWith("Finance") && type.GetMethod("<Clone>$") != null
                    || type == typeof(CertificateLayout)
                    || type == typeof(CertificateElement)
                    || type == typeof(AgendaItem)
                )
            )
                return;
            foreach (var p in type.GetProperties())
            {
                var child = p.GetValue(value);
                if (child == null && nullability.Create(p).ReadState == NullabilityState.NotNull)
                    throw new ApiError(400, p.Name + " is required.");
                Validate(child, depth + 1);
            }
        }
        foreach (var arg in context.Arguments)
            Validate(arg, 0);
        return await next(context);
    }
}
