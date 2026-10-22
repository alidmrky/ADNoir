namespace AdNoir.BuildingBlocks.Configuration;

/// <summary>How the cache is wired for this process.</summary>
public enum CacheMode
{
    /// <summary>In-memory L1 only. Valid for a single instance outside Production.</summary>
    InMemoryOnly = 0,

    /// <summary>L1 plus a Redis-backed L2 shared by every instance.</summary>
    Distributed = 1,
}

/// <summary>How messaging is wired for this process.</summary>
public enum TransportMode
{
    /// <summary>MassTransit's in-memory transport. Messages do not survive a restart.</summary>
    InMemory = 0,

    /// <summary>A real RabbitMQ broker.</summary>
    RabbitMq = 1,
}

/// <summary>
/// Decides whether infrastructure runs in its real or its fallback form, and refuses the
/// fallback in Production (fail-fast rule).
/// </summary>
/// <remarks>
/// <para>
/// Conditional registration is what lets phases 1 and 2 run with nothing installed. Its one
/// real danger is that the convenience leaks into production unnoticed: an instance without
/// Redis loses claim freshness across instances, and an instance on the in-memory transport
/// holds messages in process memory and <em>silently loses them</em> on restart. Neither
/// failure announces itself.
/// </para>
/// <para>
/// So the branch lives here, in one place, as plain functions with no dependencies - which
/// is also what makes the rule testable. The unit tests pin both directions: fallback
/// allowed outside Production, startup failure inside it.
/// </para>
/// </remarks>
public static class InfrastructureMode
{
    /// <summary>Decides the cache mode.</summary>
    /// <param name="isProduction">Whether the host environment is Production.</param>
    /// <param name="redisConnectionString">The configured Redis connection string, if any.</param>
    /// <returns>The mode to register.</returns>
    /// <exception cref="InvalidOperationException">
    /// Production was requested without a Redis connection string.
    /// </exception>
    public static CacheMode ResolveCache(bool isProduction, string? redisConnectionString)
    {
        bool configured = !string.IsNullOrWhiteSpace(redisConnectionString);

        if (configured)
        {
            return CacheMode.Distributed;
        }

        if (isProduction)
        {
            throw new InvalidOperationException(
                "AdNoir:Redis:ConnectionString is required in Production. Running on the in-memory " +
                "cache alone means a security-stamp invalidation on one instance never reaches the " +
                "others, so authorization changes are delayed silently. Falling back is allowed only " +
                "in Development and test environments.");
        }

        return CacheMode.InMemoryOnly;
    }

    /// <summary>Decides the transport mode.</summary>
    /// <param name="isProduction">Whether the host environment is Production.</param>
    /// <param name="rabbitMqHost">The configured RabbitMQ host, if any.</param>
    /// <returns>The mode to register.</returns>
    /// <exception cref="InvalidOperationException">
    /// Production was requested without a RabbitMQ host.
    /// </exception>
    public static TransportMode ResolveTransport(bool isProduction, string? rabbitMqHost)
    {
        bool configured = !string.IsNullOrWhiteSpace(rabbitMqHost);

        if (configured)
        {
            return TransportMode.RabbitMq;
        }

        if (isProduction)
        {
            throw new InvalidOperationException(
                "AdNoir:RabbitMq:Host is required in Production. The in-memory transport keeps " +
                "messages in process memory and loses them on restart without reporting anything. " +
                "Falling back is allowed only in Development and test environments.");
        }

        return TransportMode.InMemory;
    }
}
