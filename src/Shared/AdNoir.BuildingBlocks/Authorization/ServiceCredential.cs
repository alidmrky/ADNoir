using Microsoft.Extensions.Configuration;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>Dedicated service credentials, independent of end-user access tokens.</summary>
public sealed class ServiceCredential
{
    /// <summary>Service identity metadata header.</summary>
    public const string IdHeader = "x-adnoir-service-id";
    /// <summary>Service secret metadata header. Never log this header.</summary>
    public const string KeyHeader = "x-adnoir-service-key";
    /// <summary>Configured service identity.</summary>
    public string ServiceId { get; set; } = string.Empty;
    /// <summary>Base64-encoded random secret containing at least 32 bytes.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Validates the credential without including secrets in errors.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceId) || ServiceId.Length > 64 ||
            ServiceId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new InvalidOperationException("ServiceId must contain 1-64 ASCII letters, digits or hyphens.");
        }
        try
        {
            byte[] decoded = Convert.FromBase64String(Key);
            if (decoded.Length < 32 || Key.Length > 1024 || Convert.ToBase64String(decoded) != Key)
            {
                throw new InvalidOperationException("Service keys must contain at least 32 random bytes encoded as Base64.");
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Service keys must be Base64 encoded.");
        }
    }

    /// <summary>Loads the outgoing Identity client credential; missing credentials fail startup.</summary>
    public static ServiceCredential Load(IConfiguration configuration, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ServiceCredential credential = configuration.GetSection($"AdNoir:Identity:ServiceCredentials:{serviceName}")
            .Get<ServiceCredential>() ?? new ServiceCredential();
        credential.Validate();
        return credential;
    }
}
