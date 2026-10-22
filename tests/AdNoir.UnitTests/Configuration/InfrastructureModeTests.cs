using AdNoir.BuildingBlocks.Configuration;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Configuration;

/// <summary>
/// The fail-fast rule for infrastructure configuration, pinned by a test.
/// </summary>
/// <remarks>
/// The conditional infrastructure registration is what lets development run with nothing
/// installed. Its danger is that the convenience reaches production unnoticed: without Redis
/// a security-stamp invalidation never leaves the instance that made it, and on the in-memory
/// transport messages vanish on restart. Neither announces itself, so the guard has to.
/// </remarks>
public sealed class InfrastructureModeTests
{
    [Fact]
    public void Development_without_Redis_falls_back_to_the_in_memory_cache()
    {
        CacheMode mode = InfrastructureMode.ResolveCache(isProduction: false, redisConnectionString: null);

        mode.ShouldBe(CacheMode.InMemoryOnly);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Production_without_Redis_fails_at_startup(string? connectionString)
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            InfrastructureMode.ResolveCache(isProduction: true, connectionString));

        exception.Message.ShouldContain("Redis");
    }

    [Fact]
    public void A_configured_Redis_gives_the_distributed_cache_in_any_environment()
    {
        InfrastructureMode.ResolveCache(false, "localhost:6379").ShouldBe(CacheMode.Distributed);
        InfrastructureMode.ResolveCache(true, "localhost:6379").ShouldBe(CacheMode.Distributed);
    }

    [Fact]
    public void Development_without_RabbitMq_falls_back_to_the_in_memory_transport()
    {
        TransportMode mode = InfrastructureMode.ResolveTransport(isProduction: false, rabbitMqHost: null);

        mode.ShouldBe(TransportMode.InMemory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Production_without_RabbitMq_fails_at_startup(string? host)
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            InfrastructureMode.ResolveTransport(isProduction: true, host));

        exception.Message.ShouldContain("RabbitMq");
    }

    [Fact]
    public void A_configured_broker_gives_the_real_transport_in_any_environment()
    {
        InfrastructureMode.ResolveTransport(false, "rabbitmq").ShouldBe(TransportMode.RabbitMq);
        InfrastructureMode.ResolveTransport(true, "rabbitmq").ShouldBe(TransportMode.RabbitMq);
    }
}
