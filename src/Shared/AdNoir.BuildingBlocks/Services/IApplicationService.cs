namespace AdNoir.BuildingBlocks.Services;

/// <summary>
/// Marker for application services.
/// </summary>
/// <remarks>
/// <para>
/// The marker exists so the response contract can be checked by machine rather than by
/// review. An architecture test reflects over every type implementing this interface and
/// asserts that each of its public methods returns <c>Task&lt;BaseResponse&gt;</c> or
/// <c>Task&lt;BaseResponse&lt;T&gt;&gt;</c>, is asynchronous, and takes a
/// <see cref="CancellationToken"/> as its last parameter. A red test fails the build.
/// </para>
/// <para>
/// Empty interfaces are usually a smell; this one earns its place by being the anchor for
/// that test. Without it the rule would be a sentence in a document, which is the same as
/// no rule at all.
/// </para>
/// </remarks>
public interface IApplicationService;
