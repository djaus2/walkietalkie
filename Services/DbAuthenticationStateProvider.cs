using System.Security.Claims;
using WalkieTalkie.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace WalkieTalkie.Services;

public sealed class DbAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private readonly AuthService _auth;

    public DbAuthenticationStateProvider(IJSRuntime js, AuthService auth)
    {
        _js = js;
        _auth = auth;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var contextId = await _js.InvokeAsync<string>("WalkieTalkieAuth.getContextId");
            if (string.IsNullOrWhiteSpace(contextId))
            {
                return Anonymous();
            }

            var user = await _auth.GetUserByContextIdAsync(contextId, touch: true);
            if (user is null)
            {
                await _js.InvokeVoidAsync("WalkieTalkieAuth.clearContextId");
                return Anonymous();
            }

            var principal = AuthService.BuildPrincipal(user);
            return new AuthenticationState(principal);
        }
        catch
        {
            return Anonymous();
        }
    }

    public async Task<(bool Success, bool AlreadyLoggedIn, bool InvalidCredentials, string? Error)> TryLoginAsync(string name, string pin)
    {
        var result = await _auth.TryDbContextLoginAsync(name, pin);
        if (!result.Success || result.User is null || string.IsNullOrWhiteSpace(result.ContextId))
        {
            return (false, result.AlreadyLoggedIn, result.InvalidCredentials, result.Error);
        }

        await _js.InvokeVoidAsync("WalkieTalkieAuth.setContextId", result.ContextId);
        var principal = AuthService.BuildPrincipal(result.User);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
        return (true, false, false, null);
    }

    public async Task SignOutAsync(bool isTimeout = false)
    {
        try
        {
            var contextId = await _js.InvokeAsync<string>("WalkieTalkieAuth.getContextId");
            if (!string.IsNullOrWhiteSpace(contextId))
            {
                await _auth.LogoutByContextIdAsync(contextId, isTimeout);
            }
        }
        catch
        {
        }

        try
        {
            await _js.InvokeVoidAsync("WalkieTalkieAuth.clearContextId");
        }
        catch
        {
        }

        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
    }

    private static AuthenticationState Anonymous()
    {
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}
