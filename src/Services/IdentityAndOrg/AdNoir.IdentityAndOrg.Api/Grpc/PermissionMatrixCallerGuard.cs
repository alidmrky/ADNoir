using System.Net;
using System.Security.Cryptography;
using System.Text;
using AdNoir.BuildingBlocks.Authorization;
using AdNoir.Shared.Contracts.Messages;
using Grpc.Core;

namespace AdNoir.IdentityAndOrg.Api.Grpc;

/// <summary>Authenticates dedicated service credentials before any tenant data is read.</summary>
public sealed class PermissionMatrixCallerGuard
{
    private readonly PermissionMatrixCaller[] _callers;
    private readonly bool _development;

    /// <summary>Loads and validates the server-side allowlist at startup.</summary>
    public PermissionMatrixCallerGuard(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        _development = environment.IsDevelopment();
        _callers = configuration.GetSection("AdNoir:GrpcSecurity:Callers").Get<PermissionMatrixCaller[]>() ?? [];
        foreach (PermissionMatrixCaller caller in _callers)
        {
            caller.Credential.Validate();
            if (caller.TenantIds.Contains(Guid.Empty) || (caller.AllTenants && caller.TenantIds.Length != 0))
            {
                throw new InvalidOperationException("Configure either AllTenants or a non-empty-Guid tenant allowlist.");
            }
        }
        if (_callers.Select(caller => caller.Credential.ServiceId).Distinct(StringComparer.Ordinal).Count() != _callers.Length ||
            _callers.Select(caller => caller.Credential.Key).Distinct(StringComparer.Ordinal).Count() != _callers.Length)
        {
            throw new InvalidOperationException("gRPC callers must have distinct service identities and keys.");
        }
        if (!_development && _callers.Length == 0)
        {
            throw new InvalidOperationException("Configure gRPC callers before starting Identity outside Development.");
        }
    }

    /// <summary>Rejects missing credentials, insecure transport and unauthorized tenant access.</summary>
    public void Authorize(ServerCallContext context, Guid tenantId, bool validateSession = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        HttpContext http = context.GetHttpContext();
        bool localDevelopment = _development && http.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote);
        if (!http.Request.IsHttps && !localDevelopment)
        {
            throw Failure(StatusCode.Unauthenticated);
        }
        string? id = SingleHeader(context.RequestHeaders, ServiceCredential.IdHeader);
        string? key = SingleHeader(context.RequestHeaders, ServiceCredential.KeyHeader);
        PermissionMatrixCaller? caller = _callers.FirstOrDefault(item => item.Credential.ServiceId == id);
        if (caller is null || key is null || key.Length > 1024 ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)),
                SHA256.HashData(Encoding.UTF8.GetBytes(caller.Credential.Key))))
        {
            throw Failure(StatusCode.Unauthenticated);
        }
        bool permitted = validateSession ? caller.CanValidateUserSession : caller.CanReadPermissionMatrix;
        if (!permitted || (!caller.AllTenants && !caller.TenantIds.Contains(tenantId)))
        {
            throw Failure(StatusCode.PermissionDenied);
        }
    }

    private static string? SingleHeader(Metadata headers, string name)
    {
        Metadata.Entry[] values = headers.Where(entry => entry.Key == name).ToArray();
        return values.Length == 1 && !values[0].IsBinary ? values[0].Value : null;
    }

    private static RpcException Failure(StatusCode status) => new(new Status(status, MessageCodes.Auth.PermissionDenied),
        new Metadata { { PermissionMatrixGrpcService.ErrorCodeTrailer, MessageCodes.Auth.PermissionDenied } });
}
