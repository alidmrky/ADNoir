using System.Security.Cryptography;
using System.Text;
using AdNoir.BuildingBlocks.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AdNoir.IdentityAndOrg.Infrastructure.Security;

/// <summary>Supplies the RSA key tokens are signed with, and its public half as a JWKS.</summary>
public interface ISigningKeyProvider
{
    /// <summary>The private key used for signing.</summary>
    RsaSecurityKey SigningKey { get; }

    /// <summary>The key identifier written into every token header as <c>kid</c>.</summary>
    string KeyId { get; }

    /// <summary>The public key set served at <c>/.well-known/jwks.json</c>.</summary>
    /// <returns>The JWKS as an object ready for serialization.</returns>
    JsonWebKeySet GetPublicKeySet();
}

/// <summary>
/// RS256 key management.
/// </summary>
/// <remarks>
/// <para>
/// The private key lives only in this service. Other services never receive it; they fetch
/// the public half from the JWKS endpoint and select it by <c>kid</c>, which is what makes
/// key rotation possible without redeploying every consumer.
/// </para>
/// <para>
/// The key identifier is derived from the public key itself rather than being random, so the
/// same key always produces the same <c>kid</c> across restarts and instances. A random one
/// would make cached JWKS entries stop matching after every restart.
/// </para>
/// </remarks>
public sealed class RsaSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private readonly RSA _rsa;

    /// <summary>Loads the configured key, or generates one outside Production.</summary>
    /// <param name="options">JWT settings.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="InvalidOperationException">
    /// Production was requested without a configured private key.
    /// </exception>
    public RsaSigningKeyProvider(
        IOptions<JwtOptions> options,
        IHostEnvironment environment,
        ILogger<RsaSigningKeyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        string? pem = options.Value.PrivateKeyPem;

        _rsa = RSA.Create();

        if (!string.IsNullOrWhiteSpace(pem))
        {
            _rsa.ImportFromPem(pem);
        }
        else if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "AdNoir:Jwt:PrivateKeyPem is required in Production. Generating a key per instance " +
                "would mean a token signed by one instance is rejected by another, intermittently " +
                "and with no clear cause.");
        }
        else
        {
            _rsa.KeySize = 2048;

            logger.LogWarning(
                "No signing key configured; generated an ephemeral RSA key for {Environment}. " +
                "Every token issued now becomes invalid when this process restarts. Set " +
                "AdNoir__Jwt__PrivateKeyPem to keep sessions across restarts.",
                environment.EnvironmentName);
        }

        RSAParameters publicParameters = _rsa.ExportParameters(includePrivateParameters: false);

        KeyId = ComputeKeyId(publicParameters);
        SigningKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };
    }

    /// <inheritdoc />
    public RsaSecurityKey SigningKey { get; }

    /// <inheritdoc />
    public string KeyId { get; }

    /// <inheritdoc />
    public JsonWebKeySet GetPublicKeySet()
    {
        RSA publicOnly = RSA.Create();
        publicOnly.ImportParameters(_rsa.ExportParameters(includePrivateParameters: false));

        RsaSecurityKey publicKey = new(publicOnly) { KeyId = KeyId };

        JsonWebKey jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        jsonWebKey.Use = "sig";
        jsonWebKey.Alg = SecurityAlgorithms.RsaSha256;

        JsonWebKeySet keySet = new();
        keySet.Keys.Add(jsonWebKey);

        return keySet;
    }

    /// <inheritdoc />
    public void Dispose() => _rsa.Dispose();

    /// <summary>
    /// Derives a stable identifier from the public key: the RFC 7638 style thumbprint of its
    /// modulus and exponent, base64url encoded.
    /// </summary>
    private static string ComputeKeyId(RSAParameters parameters)
    {
        string modulus = Base64UrlEncoder.Encode(parameters.Modulus!);
        string exponent = Base64UrlEncoder.Encode(parameters.Exponent!);

        string canonical = $$"""{"e":"{{exponent}}","kty":"RSA","n":"{{modulus}}"}""";

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));

        return Base64UrlEncoder.Encode(digest);
    }
}
