using System.Globalization;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// Runs FluentValidation for a request type before the handler executes.
/// </summary>
/// <typeparam name="TRequest">The request type to validate.</typeparam>
/// <remarks>
/// <para>
/// Without CQRS there is no pipeline behaviour to hang validation on, so it lives in an
/// endpoint filter. Handlers therefore never begin with a validation block.
/// </para>
/// <para>
/// Validators declare failures with <c>WithErrorCode(MessageCodes...)</c>, never with
/// <c>WithMessage</c>: the text belongs to the catalog, so it can be translated and reviewed
/// like every other user-facing string.
/// </para>
/// </remarks>
public sealed class ValidationEndpointFilter<TRequest>(IValidator<TRequest>? validator) : IEndpointFilter
    where TRequest : class
{
    private readonly IValidator<TRequest>? _validator = validator;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (_validator is null)
        {
            return await next(context);
        }

        TRequest? request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return await next(context);
        }

        ValidationResult result = await _validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (result.IsValid)
        {
            return await next(context);
        }

        IReadOnlyList<ResponseMessage> errors =
            [.. result.Errors.Select(ToResponseMessage)];

        return BaseResponse.ValidationFailed(errors).ToHttpResult();
    }

    private static ResponseMessage ToResponseMessage(ValidationFailure failure)
    {
        // An empty error code means a validator used WithMessage instead of WithErrorCode.
        // Falling back keeps the response well-formed; the catalog integrity test and review
        // are what stop the shortcut from spreading.
        string code = string.IsNullOrWhiteSpace(failure.ErrorCode)
            ? MessageCodes.Validation.InvalidFormat
            : failure.ErrorCode;

        return new ResponseMessage(
            code,
            MessageSeverity.Error,
            Field: ToCamelCase(failure.PropertyName),
            Args: ToArgs(failure));
    }

    /// <summary>
    /// Translates FluentValidation's placeholder names into the catalog's own. FluentValidation
    /// uses <c>PropertyName</c>, <c>MaxLength</c> and so on; our entries read <c>{field}</c>,
    /// <c>{maxLength}</c>. Without this mapping the placeholders would survive into the text
    /// the user sees.
    /// </summary>
    private static Dictionary<string, object?>? ToArgs(ValidationFailure failure)
    {
        if (failure.FormattedMessagePlaceholderValues is not { Count: > 0 } placeholders)
        {
            return null;
        }

        Dictionary<string, object?> args = new(placeholders.Count, StringComparer.Ordinal);

        foreach ((string key, object value) in placeholders)
        {
            string name = key switch
            {
                "PropertyName" => "field",
                "PropertyPath" => "fieldPath",
                "ComparisonValue" => "comparison",
                _ => ToCamelCase(key),
            };

            // FluentValidation also passes PropertyValue, which can be the very input that was
            // rejected. Echoing it back into a message risks reflecting unvalidated content.
            if (string.Equals(key, "PropertyValue", StringComparison.Ordinal))
            {
                continue;
            }

            args[name] = value;
        }

        return args.Count == 0 ? null : args;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return string.Create(
            value.Length,
            value,
            static (span, source) =>
            {
                source.AsSpan().CopyTo(span);
                span[0] = char.ToLower(source[0], CultureInfo.InvariantCulture);
            });
    }
}
