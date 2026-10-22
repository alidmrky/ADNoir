namespace AdNoir.BuildingBlocks.Configuration;

/// <summary>MongoDB settings, bound from <c>AdNoir:Mongo</c>.</summary>
public sealed class MongoOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:Mongo";

    /// <summary>
    /// Connection string. Must point at a replica set even for a single node: the
    /// transactional outbox uses multi-document transactions, which standalone mongod
    /// does not support.
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/?replicaSet=rs0";

    /// <summary>Database name.</summary>
    public string DatabaseName { get; set; } = "adnoir";
}

/// <summary>Redis settings, bound from <c>AdNoir:Redis</c>.</summary>
public sealed class RedisOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:Redis";

    /// <summary>
    /// Connection string. Empty means no L2 cache: outside Production the application runs
    /// on the in-memory L1 only. In Production an empty value is a startup failure, because
    /// a single instance without L2 loses claim-freshness guarantees.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Key prefix used to keep instances apart in a shared Redis.</summary>
    public string InstanceName { get; set; } = "adnoir";
}

/// <summary>RabbitMQ settings, bound from <c>AdNoir:RabbitMq</c>.</summary>
public sealed class RabbitMqOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:RabbitMq";

    /// <summary>
    /// Broker host. Empty means MassTransit's in-memory transport, which is fine for
    /// development and tests but loses messages on restart - so in Production an empty
    /// value is a startup failure rather than a silent downgrade.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>AMQP TCP port.</summary>
    public ushort Port { get; set; } = 5672;

    /// <summary>Virtual host.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Username.</summary>
    public string Username { get; set; } = "adnoir";

    /// <summary>Password.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>JWT settings, bound from <c>AdNoir:Jwt</c>.</summary>
public sealed class JwtOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:Jwt";

    /// <summary>Token issuer.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token audience.</summary>
    public string Audience { get; set; } = "adnoir";

    /// <summary>Access token lifetime in minutes. Plan G.1: five minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 5;

    /// <summary>Refresh token lifetime in days. Plan G.1: seven days, rotating.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// JWKS endpoint. The private key stays in Identity; every other service fetches the
    /// public key from here and matches it by <c>kid</c>.
    /// </summary>
    public string JwksUri { get; set; } = string.Empty;

    /// <summary>
    /// RSA private key in PEM form, used to sign tokens. Set on the Identity service only.
    /// </summary>
    /// <remarks>
    /// Empty outside Production means a key is generated at startup, which is convenient but
    /// invalidates every issued token on restart. In Production an empty value is a startup
    /// failure: generating a key per instance would mean tokens signed by one instance are
    /// rejected by the next, intermittently and without an obvious cause.
    /// </remarks>
    public string? PrivateKeyPem { get; set; }
}

/// <summary>Observability settings, bound from <c>AdNoir:Otel</c>.</summary>
public sealed class ObservabilityOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:Otel";

    /// <summary>Service name reported to traces and logs.</summary>
    public string ServiceName { get; set; } = "adnoir";

    /// <summary>OTLP endpoint. Empty disables the exporter.</summary>
    public string? OtlpEndpoint { get; set; }
}
