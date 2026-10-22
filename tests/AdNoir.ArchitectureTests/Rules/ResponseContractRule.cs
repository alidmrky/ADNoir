using System.Reflection;
using AdNoir.BuildingBlocks.Services;
using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.ArchitectureTests.Rules;

/// <summary>
/// The response contract and the DTO rule, expressed once so the same
/// logic checks both the real assemblies and the probe samples.
/// </summary>
public static class ResponseContractRule
{
    /// <summary>Finds every application service type in an assembly.</summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>Types implementing <see cref="IApplicationService"/>.</returns>
    public static IReadOnlyList<Type> ServiceTypesIn(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return
        [
            .. assembly
                .GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false }
                               && typeof(IApplicationService).IsAssignableFrom(type))
        ];
    }

    /// <summary>
    /// The public methods a service declares itself.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <returns>The methods subject to the contract.</returns>
    /// <remarks>
    /// Read literally, "all public methods" would include
    /// <see cref="object.Equals(object?)"/> and property getters, and the rule would fail on
    /// every service ever written. Declared members only, minus compiler-generated accessors.
    /// </remarks>
    public static IReadOnlyList<MethodInfo> ContractMethodsOf(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return
        [
            .. serviceType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object))
        ];
    }

    /// <summary>Checks one method against the response contract.</summary>
    /// <param name="method">The method to check.</param>
    /// <returns>Descriptions of every violation, empty when the method complies.</returns>
    public static IReadOnlyList<string> ContractViolations(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        List<string> violations = [];
        string name = $"{method.DeclaringType?.Name}.{method.Name}";

        if (!IsEnvelopeTask(method.ReturnType))
        {
            violations.Add(
                $"{name} returns {Describe(method.ReturnType)}; it must return Task<BaseResponse> " +
                "or Task<BaseResponse<T>>.");
        }

        ParameterInfo[] parameters = method.GetParameters();

        if (parameters.Length == 0 || parameters[^1].ParameterType != typeof(CancellationToken))
        {
            violations.Add(
                $"{name} does not take a CancellationToken as its last parameter.");
        }

        return violations;
    }

    /// <summary>Checks one method against the DTO rule.</summary>
    /// <param name="method">The method to check.</param>
    /// <returns>Descriptions of every violation, empty when the method complies.</returns>
    /// <remarks>
    /// Worded as "no type in Shared.Contracts may reference a type from a Domain assembly",
    /// this rule would be unenforceable in practice: Shared.Contracts references no project
    /// at all, so the compiler already guarantees it and the test could never fail. What the
    /// rule is actually for - services must not return domain entities - is what is checked
    /// here, by looking at the payload type rather than at assembly references.
    /// </remarks>
    public static IReadOnlyList<string> PayloadViolations(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (!IsEnvelopeTask(method.ReturnType))
        {
            // Already reported by ContractViolations; no point saying it twice.
            return [];
        }

        Type envelope = method.ReturnType.GetGenericArguments()[0];

        if (!envelope.IsGenericType)
        {
            return [];
        }

        Type payload = envelope.GetGenericArguments()[0];

        List<string> violations = [];

        foreach (Type candidate in ExpandGenericArguments(payload))
        {
            if (typeof(IEntity).IsAssignableFrom(candidate))
            {
                violations.Add(
                    $"{method.DeclaringType?.Name}.{method.Name} returns the domain entity " +
                    $"{candidate.Name} instead of a DTO. Returning entities turns the " +
                    "database schema into the API contract and leaks fields such as IsDeleted, " +
                    "TenantId and password hashes.");
            }
        }

        return violations;
    }

    private static bool IsEnvelopeTask(Type returnType)
    {
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return false;
        }

        Type result = returnType.GetGenericArguments()[0];

        return typeof(BaseResponse).IsAssignableFrom(result);
    }

    /// <summary>
    /// Yields a type and, recursively, its generic arguments - so
    /// <c>IReadOnlyList&lt;UserEntity&gt;</c> is caught as readily as a bare entity.
    /// </summary>
    private static IEnumerable<Type> ExpandGenericArguments(Type type)
    {
        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in ExpandGenericArguments(argument))
            {
                yield return nested;
            }
        }
    }

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<...>"
            : type.Name;
}
