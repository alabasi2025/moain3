using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Hrms.Api.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } =
        permission ?? throw new ArgumentNullException(nameof(permission));
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        bool isAdmin = context.User.Claims.Any(claim =>
            string.Equals(claim.Type, "is_admin", StringComparison.Ordinal) &&
            string.Equals(claim.Value, "true", StringComparison.Ordinal));

        bool hasPermission = context.User.Claims.Any(claim =>
            string.Equals(claim.Type, "perm", StringComparison.Ordinal) &&
            string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal));

        if (isAdmin || hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string Prefix = "perm:";
    private readonly DefaultAuthorizationPolicyProvider _defaultProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _defaultProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return _defaultProvider.GetPolicyAsync(policyName);
        }

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _defaultProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _defaultProvider.GetFallbackPolicyAsync();
    }
}

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute("perm:" + permission)
{
}
