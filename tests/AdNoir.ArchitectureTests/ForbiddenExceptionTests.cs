using System.Reflection;
using AdNoir.ArchitectureTests.Il;
using AdNoir.ArchitectureTests.Samples;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

/// <summary>
/// Plan C.4: the runtime's own exception types are never thrown deliberately.
/// </summary>
/// <remarks>
/// Throwing <see cref="NullReferenceException"/> as a signal makes a real null-reference bug
/// indistinguishable from an intentional one, and the same goes for the rest of this list.
/// The check reads method bodies, because whether a type is thrown is a property of the code
/// inside a method, not of the method's signature.
/// </remarks>
public sealed class ForbiddenExceptionTests
{
    private static readonly HashSet<string> ForbiddenTypes = new(StringComparer.Ordinal)
    {
        "System.NullReferenceException",
        "System.IndexOutOfRangeException",
        "System.AccessViolationException",
        "System.StackOverflowException",
        "System.OutOfMemoryException",
    };

    [Fact]
    public void Rule_detects_a_forbidden_throw()
    {
        // Proves the IL scan works. Without this the rule below could pass because the scanner
        // silently found nothing - a green test that checks nothing.
        IReadOnlyList<IlViolation> violations =
            IlScanner.FindConstructionOf(typeof(RuleProbes).Assembly, ForbiddenTypes);

        violations.ShouldContain(
            violation => violation.MethodName == nameof(RuleProbes.ForbiddenThrowProbe.ThrowsNullReference),
            "the probe throws NullReferenceException on purpose");

        violations.ShouldContain(
            violation => violation.MethodName == nameof(RuleProbes.ForbiddenThrowProbe.ThrowsIndexOutOfRange),
            "the probe throws IndexOutOfRangeException on purpose");
    }

    [Fact]
    public void Production_code_never_throws_a_runtime_owned_exception()
    {
        List<IlViolation> violations = [];

        foreach (Assembly assembly in AdNoirAssemblies.All())
        {
            violations.AddRange(IlScanner.FindConstructionOf(assembly, ForbiddenTypes));
        }

        violations.ShouldBeEmpty(
            "NullReferenceException, IndexOutOfRangeException, AccessViolationException, " +
            "StackOverflowException and OutOfMemoryException belong to the runtime. Throwing one " +
            "deliberately makes a genuine bug and an intentional signal look identical in a crash " +
            $"report.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }
}
