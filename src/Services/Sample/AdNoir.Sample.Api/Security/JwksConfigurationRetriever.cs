using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AdNoir.Sample.Api.Security;

/// <summary>Loads signing keys directly from Identity's JWKS endpoint.</summary>
public sealed class JwksConfigurationRetriever(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    /// <inheritdoc />
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        string document = await retriever.GetDocumentAsync(address, cancel);
        JsonWebKeySet keySet = new(document);
        OpenIdConnectConfiguration configuration = new() { Issuer = issuer, JwksUri = address };
        foreach (SecurityKey key in keySet.GetSigningKeys())
        {
            configuration.SigningKeys.Add(key);
        }

        if (configuration.SigningKeys.Count == 0)
        {
            throw new InvalidOperationException("Identity JWKS contains no usable signing keys.");
        }

        return configuration;
    }
}
