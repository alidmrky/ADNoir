using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AdNoir.ArchitectureTests.Il;

/// <summary>One method that violated an IL-level rule.</summary>
/// <param name="AssemblyName">The assembly the method lives in.</param>
/// <param name="TypeName">The declaring type's full name.</param>
/// <param name="MethodName">The method name.</param>
/// <param name="Detail">What was found.</param>
public sealed record IlViolation(string AssemblyName, string TypeName, string MethodName, string Detail)
{
    /// <inheritdoc />
    public override string ToString() => $"{TypeName}.{MethodName} -> {Detail} [{AssemblyName}]";
}

/// <summary>
/// Reads method bodies to enforce the two rules NetArchTest cannot express.
/// </summary>
/// <remarks>
/// NetArchTest works at type and namespace level; it has no view of what a method actually
/// does. Both "never throw NullReferenceException" and "every cross-tenant call
/// must be justified" are statements about method bodies, so they need IL
/// inspection. Mono.Cecil is already a transitive dependency of NetArchTest, so this adds no
/// new third-party surface.
/// </remarks>
public static class IlScanner
{
    /// <summary>Finds methods that construct any of the given types.</summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="typeFullNames">Full names of the forbidden types.</param>
    /// <returns>The violations found.</returns>
    public static IReadOnlyList<IlViolation> FindConstructionOf(Assembly assembly, ISet<string> typeFullNames)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(typeFullNames);

        List<IlViolation> violations = [];

        foreach ((TypeDefinition type, MethodDefinition method) in MethodsOf(assembly))
        {
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.OpCode != OpCodes.Newobj ||
                    instruction.Operand is not MethodReference constructor)
                {
                    continue;
                }

                string constructedType = constructor.DeclaringType.FullName;

                if (typeFullNames.Contains(constructedType))
                {
                    violations.Add(new IlViolation(
                        assembly.GetName().Name ?? "?",
                        type.FullName,
                        method.Name,
                        $"constructs {constructedType}"));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Finds methods that call into a type, optionally ignoring those carrying an attribute.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="declaringTypeFullName">
    /// Full name of the called type. Generic types are matched on their open form, e.g.
    /// <c>Namespace.IThing`1</c>.
    /// </param>
    /// <param name="exemptingAttributeFullName">
    /// A method carrying this attribute is not reported. Pass null to report every caller.
    /// </param>
    /// <returns>The violations found.</returns>
    public static IReadOnlyList<IlViolation> FindCallsInto(
        Assembly assembly,
        string declaringTypeFullName,
        string? exemptingAttributeFullName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaringTypeFullName);

        List<IlViolation> violations = [];

        foreach ((TypeDefinition type, MethodDefinition method) in MethodsOf(assembly))
        {
            // A type that implements the interface is defining the operation, not consuming
            // it; reporting the implementation itself would make the rule unusable.
            if (Implements(type, declaringTypeFullName))
            {
                continue;
            }

            if (exemptingAttributeFullName is not null && HasMethodOrAsyncOriginAttribute(type, method, exemptingAttributeFullName))
            {
                continue;
            }

            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.OpCode != OpCodes.Call &&
                    instruction.OpCode != OpCodes.Callvirt)
                {
                    continue;
                }

                if (instruction.Operand is not MethodReference called)
                {
                    continue;
                }

                string calledType = NormalizeTypeName(called.DeclaringType);

                if (string.Equals(calledType, declaringTypeFullName, StringComparison.Ordinal))
                {
                    violations.Add(new IlViolation(
                        assembly.GetName().Name ?? "?",
                        type.FullName,
                        method.Name,
                        $"calls {calledType}.{called.Name}"));
                }
            }
        }

        return violations;
    }

    private static IEnumerable<(TypeDefinition Type, MethodDefinition Method)> MethodsOf(Assembly assembly)
    {
        using ModuleDefinition module = ModuleDefinition.ReadModule(assembly.Location);

        foreach (TypeDefinition type in AllTypes(module.Types))
        {
            if (IsCompilerInternalType(type))
            {
                continue;
            }

            foreach (MethodDefinition method in type.Methods)
            {
                if (method.HasBody)
                {
                    yield return (type, method);
                }
            }
        }
    }

    /// <summary>
    /// Whether a type is one the compiler emits for its own use, containing no user code.
    /// </summary>
    /// <remarks>
    /// The distinction matters and is narrower than "compiler-generated". Roslyn emits two
    /// different kinds of synthesized type:
    /// <list type="bullet">
    /// <item>
    /// Internal helpers named <c>&lt;&gt;something</c> - the collection-expression backing
    /// types, for instance, whose indexers legitimately throw
    /// <see cref="IndexOutOfRangeException"/>. No line of our source is in them, so scanning
    /// them reports the compiler's decisions as ours.
    /// </item>
    /// <item>
    /// State machines named <c>&lt;MethodName&gt;d__0</c>, which are where the body of every
    /// async method actually lives. Skipping those would exempt all async code from these
    /// rules - most of the codebase - so the filter deliberately keeps them.
    /// </item>
    /// </list>
    /// The test for the leading <c>&lt;&gt;</c> is exactly this split: a synthesized type that
    /// carries user code always has the originating member's name between the angle brackets.
    /// </remarks>
    private static bool IsCompilerInternalType(TypeDefinition type) =>
        type.Name.StartsWith("<>", StringComparison.Ordinal);

    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (TypeDefinition type in types)
        {
            yield return type;

            foreach (TypeDefinition nested in AllTypes(type.NestedTypes))
            {
                yield return nested;
            }
        }
    }

    private static bool Implements(TypeDefinition type, string interfaceFullName) =>
        type.HasInterfaces &&
        type.Interfaces.Any(implemented =>
            string.Equals(
                NormalizeTypeName(implemented.InterfaceType),
                interfaceFullName,
                StringComparison.Ordinal));

    private static bool HasMethodOrAsyncOriginAttribute(TypeDefinition type, MethodDefinition method, string attributeFullName)
    {
        if (HasAttribute(method, attributeFullName))
        {
            return true;
        }
        // Async bodies live in MoveNext; only the exact compiler-linked source method may authorize them.
        return method.Name == "MoveNext" && type.DeclaringType is not null &&
            type.DeclaringType.Methods.Any(origin => HasAttribute(origin, attributeFullName) &&
                origin.CustomAttributes.Any(attribute =>
                    attribute.AttributeType.FullName == "System.Runtime.CompilerServices.AsyncStateMachineAttribute" &&
                    attribute.ConstructorArguments.Count == 1 &&
                    attribute.ConstructorArguments[0].Value is TypeReference stateMachine &&
                    NormalizeTypeName(stateMachine) == type.FullName));
    }

    private static bool HasAttribute(MethodDefinition method, string attributeFullName) =>
        method.HasCustomAttributes &&
        method.CustomAttributes.Any(attribute =>
            string.Equals(attribute.AttributeType.FullName, attributeFullName, StringComparison.Ordinal));

    /// <summary>
    /// Reduces a closed generic reference such as <c>IThing`1&lt;Foo&gt;</c> to its open form
    /// <c>IThing`1</c>, so one rule covers every instantiation.
    /// </summary>
    private static string NormalizeTypeName(TypeReference type) =>
        type is GenericInstanceType generic
            ? generic.ElementType.FullName
            : type.FullName;
}
