namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// One error, warning or informational message. There is a single type for all three:
/// every message is identified by a hierarchical catalog code, never by a
/// literal string, so the same key serves both machine handling and translation.
/// </summary>
/// <param name="Code">
/// Catalog key in <c>{module}.{entity}.{state}</c> form, e.g. <c>identity.user.not_found</c>.
/// Always comes from a <see cref="Messages.MessageCodes"/> constant, never a literal.
/// </param>
/// <param name="Severity">Whether this message failed the operation or merely annotates it.</param>
/// <param name="Message">
/// Human-readable text resolved from the catalog at the boundary. Service code leaves this
/// null; filling it in is the responsibility of the response writer.
/// </param>
/// <param name="Field">
/// Property path this message applies to, for validation errors. Null for messages that
/// are not tied to a single input field.
/// </param>
/// <param name="Args">
/// Values for the named placeholders in the catalog entry, e.g. <c>{userId}</c>.
/// Placeholders are named, never positional.
/// </param>
public sealed record ResponseMessage(
    string Code,
    MessageSeverity Severity,
    string? Message = null,
    string? Field = null,
    IReadOnlyDictionary<string, object?>? Args = null)
{
    /// <summary>Creates an error message for the given catalog code.</summary>
    /// <param name="code">Catalog key, from a <see cref="Messages.MessageCodes"/> constant.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A message with <see cref="MessageSeverity.Error"/>.</returns>
    public static ResponseMessage Error(string code, params (string Key, object? Value)[] args) =>
        new(code, MessageSeverity.Error, Args: ResponseArgs.From(args));

    /// <summary>Creates an error message attached to a specific input field.</summary>
    /// <param name="code">Catalog key, from a <see cref="Messages.MessageCodes"/> constant.</param>
    /// <param name="field">Property path the error applies to.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A field-scoped message with <see cref="MessageSeverity.Error"/>.</returns>
    public static ResponseMessage ForField(string code, string field, params (string Key, object? Value)[] args) =>
        new(code, MessageSeverity.Error, Field: field, Args: ResponseArgs.From(args));

    /// <summary>Creates a warning message for the given catalog code.</summary>
    /// <param name="code">Catalog key, from a <see cref="Messages.MessageCodes"/> constant.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A message with <see cref="MessageSeverity.Warning"/>.</returns>
    public static ResponseMessage Warning(string code, params (string Key, object? Value)[] args) =>
        new(code, MessageSeverity.Warning, Args: ResponseArgs.From(args));

    /// <summary>Creates an informational message for the given catalog code.</summary>
    /// <param name="code">Catalog key, from a <see cref="Messages.MessageCodes"/> constant.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A message with <see cref="MessageSeverity.Info"/>.</returns>
    public static ResponseMessage Info(string code, params (string Key, object? Value)[] args) =>
        new(code, MessageSeverity.Info, Args: ResponseArgs.From(args));

    /// <summary>
    /// Returns a copy carrying the resolved text. Used by the response writer once the
    /// catalog lookup has happened; service code never calls this.
    /// </summary>
    /// <param name="resolvedText">Text resolved from the message catalog.</param>
    /// <returns>A copy of this message with <see cref="Message"/> populated.</returns>
    public ResponseMessage WithResolvedText(string? resolvedText) =>
        this with { Message = resolvedText };
}
