using System.Security.Claims;
using System.Threading;
using WalkieTalkie.Data;
using Microsoft.EntityFrameworkCore;

namespace WalkieTalkie.Services;

public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly AppLogService _appLog;
    private static readonly SemaphoreSlim LoginStateGuard = new(1, 1);

    public AuthService(AppDbContext db, AppLogService appLog)
    {
        _db = db;
        _appLog = appLog;
    }

    public sealed record DbContextLoginResult(
        bool Success,
        bool AlreadyLoggedIn,
        bool InvalidCredentials,
        string? ContextId,
        AppUser? User,
        string? Error);

    public sealed record LoggedInGroupMemberDto(
        int GroupId,
        string GroupName,
        int UserId,
        string UserName);

    public async Task<AppUser?> ValidateLoginAsync(string name, string pin)
    {
        name = (name ?? string.Empty).Trim();
        var lookup = name.ToUpperInvariant();

        System.Diagnostics.Debug.WriteLine($"Login attempt: Name='{name}', PIN='{pin}'");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Name.ToUpper() == lookup);
        if (user is null) 
        {
            System.Diagnostics.Debug.WriteLine($"Login debug: User '{name}' not found");
            return null;
        }

        var pinValid = PinHasher.VerifyPin(pin, user.PinSalt, user.PinHash);
        System.Diagnostics.Debug.WriteLine($"Login debug: User '{name}' (ID: {user.Id}) found, PIN valid: {pinValid}");
        
        if (!pinValid) 
        {
            // Debug: Test if PIN format is valid
            var pinFormatValid = IsValidPin(pin);
            System.Diagnostics.Debug.WriteLine($"Login debug: PIN format valid: {pinFormatValid} (length: {pin?.Length})");
            return null;
        }
        
        // Log successful login
        _ = _appLog.LogPlayerActionAsync(LogType.PlayerLogin, user.Id, $"Player {user.Name} logged in");
        
        return user;
    }

    public Task<List<AppUser>> ListUsersAsync()
        => _db.Users.AsNoTracking().OrderBy(x => x.Name).ToListAsync();

    public async Task<List<AppUser>> ListAdminUsersAsync()
    {
        return await _db.Users
            .AsNoTracking()
            .Where(x => x.Roles != null && x.Roles.Split(',').Contains("Admin"))
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public Task<AppUser?> GetUserByIdAsync(int userId)
        => _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);

    public Task<AppUser?> GetUserByNameAsync(string name)
        => _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Name.ToUpper() == name.ToUpper());

    public async Task<string?> GetPreferredTtsVoiceAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        return user?.PreferredTtsVoice;
    }

    public async Task<bool> UpdatePreferredTtsVoiceAsync(int userId, string? preferredVoice)
    {
        preferredVoice = (preferredVoice ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(preferredVoice)) preferredVoice = null;
        if (preferredVoice is not null && preferredVoice.Length > 1024) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.PreferredTtsVoice = preferredVoice;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetPreferredTtsLanguageAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        return user?.PreferredTtsLanguage;
    }

    public async Task<bool> UpdatePreferredTtsLanguageAsync(int userId, string? language)
    {
        language = (language ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(language)) language = null;
        if (language is not null && language.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.PreferredTtsLanguage = language;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetPreferredTtsRegionAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        return user?.PreferredTtsRegion;
    }

    public async Task<bool> UpdatePreferredTtsRegionAsync(int userId, string? region)
    {
        region = (region ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(region)) region = null;
        if (region is not null && region.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.PreferredTtsRegion = region;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetAdminDesktopFallbackTtsVoiceAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        return user?.AdminDesktopFallbackTtsVoice;
    }

    public async Task<bool> UpdateAdminDesktopFallbackTtsVoiceAsync(int userId, string? preferredVoice)
    {
        preferredVoice = (preferredVoice ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(preferredVoice)) preferredVoice = null;
        if (preferredVoice is not null && preferredVoice.Length > 1024) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.AdminDesktopFallbackTtsVoice = preferredVoice;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetAdminDesktopFallbackTtsLanguageAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        return user?.AdminDesktopFallbackTtsLanguage;
    }

    public async Task<bool> UpdateAdminDesktopFallbackTtsLanguageAsync(int userId, string? language)
    {
        language = (language ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(language)) language = null;
        if (language is not null && language.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.AdminDesktopFallbackTtsLanguage = language;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetAdminDesktopFallbackTtsRegionAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        return user?.AdminDesktopFallbackTtsRegion;
    }

    public async Task<bool> UpdateAdminDesktopFallbackTtsRegionAsync(int userId, string? region)
    {
        region = (region ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(region)) region = null;
        if (region is not null && region.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.AdminDesktopFallbackTtsRegion = region;
        await _db.SaveChangesAsync();
        return true;
    }

    // Group management methods
    public async Task<List<Group>> ListGroupsAsync()
        => await _db.Groups.Include(g => g.OwnerUser).Include(g => g.Members).ThenInclude(m => m.User).OrderBy(g => g.Name).ToListAsync();

    public async Task<List<AppUser>> ListPlayersAsync()
    {
        var users = await _db.Users.AsNoTracking().Where(u => !string.IsNullOrWhiteSpace(u.Roles)).ToListAsync();
        return users.Where(u => u.Roles.Contains("Player", StringComparison.OrdinalIgnoreCase) && !u.Roles.Contains("Admin", StringComparison.OrdinalIgnoreCase)).OrderBy(u => u.Name).ToList();
    }

    public async Task<List<Group>> GetUserGroupsAsync(int userId)
        => await _db.Groups.AsNoTracking().Include(g => g.OwnerUser).Include(g => g.Members).Where(g => g.Members.Any(m => m.UserId == userId)).OrderBy(g => g.Name).ToListAsync();

    public async Task<List<LoggedInGroupMemberDto>> GetLoggedInUsersInUserGroupsAsync(int userId)
    {
        await LoginStateGuard.WaitAsync();
        try
        {
            var myGroupIds = await _db.GroupMembers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.GroupId)
                .Distinct()
                .ToListAsync();

            if (myGroupIds.Count == 0)
            {
                return new List<LoggedInGroupMemberDto>();
            }

            var activeUserIds = await _db.LoginContexts
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();

            if (activeUserIds.Count == 0)
            {
                return new List<LoggedInGroupMemberDto>();
            }

            var groupMembers = await _db.GroupMembers
                .AsNoTracking()
                .Where(x => myGroupIds.Contains(x.GroupId) && x.UserId != userId)
                .Select(x => new { x.GroupId, x.UserId })
                .ToListAsync();

            var activeUserIdSet = activeUserIds.ToHashSet();
            var activeMembers = groupMembers
                .Where(x => activeUserIdSet.Contains(x.UserId))
                .ToList();

            if (activeMembers.Count == 0)
            {
                return new List<LoggedInGroupMemberDto>();
            }

            var groupIds = activeMembers.Select(x => x.GroupId).Distinct().ToList();
            var userIds = activeMembers.Select(x => x.UserId).Distinct().ToList();

            var groupNames = await _db.Groups
                .AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            var userNames = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            var result = activeMembers
                .Where(x => groupNames.ContainsKey(x.GroupId) && userNames.ContainsKey(x.UserId))
                .Select(x => new LoggedInGroupMemberDto(
                    x.GroupId,
                    groupNames[x.GroupId],
                    x.UserId,
                    userNames[x.UserId]))
                .Distinct()
                .OrderBy(x => x.GroupName)
                .ThenBy(x => x.UserName)
                .ToList();

            return result;
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task<Group?> CreateGroupAsync(string name, string? description, int ownerUserId)
    {
        // Check if group name already exists
        var existing = await _db.Groups.FirstOrDefaultAsync(g => g.Name == name);
        if (existing != null) return null;

        var group = new Group
        {
            Name = name,
            Description = description,
            OwnerUserId = ownerUserId
        };

        _db.Groups.Add(group);
        await _db.SaveChangesAsync();

        // Add owner as a member
        await AddGroupMemberAsync(group.Id, ownerUserId);

        return group;
    }

    public async Task<bool> DeleteGroupAsync(int groupId, int adminUserId, string adminPin)
    {
        var admin = await _db.Users.SingleOrDefaultAsync(x => x.Id == adminUserId);
        if (admin is null || !HasRole(admin, "Admin")) return false;
        if (!PinHasher.VerifyPin(adminPin, admin.PinSalt, admin.PinHash)) return false;

        var group = await _db.Groups.FindAsync(groupId);
        if (group == null) return false;

        // Remove all members first
        var members = await _db.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
        _db.GroupMembers.RemoveRange(members);

        // Delete the group
        _db.Groups.Remove(group);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<List<GroupMember>> GetGroupMembersAsync(int groupId)
        => await _db.GroupMembers.Include(m => m.User).Where(m => m.GroupId == groupId).OrderBy(m => m.User != null ? m.User.Name : "").ToListAsync();

    public async Task<bool> AddGroupMemberAsync(int groupId, int userId)
    {
        // Check if already a member
        var existing = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (existing != null) return false;

        var member = new GroupMember
        {
            GroupId = groupId,
            UserId = userId
        };

        _db.GroupMembers.Add(member);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RemoveGroupMemberAsync(int groupId, int userId)
    {
        var member = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (member == null) return false;

        _db.GroupMembers.Remove(member);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ChangePinAsync(int userId, string currentPin, string newPin)
    {
        currentPin = (currentPin ?? string.Empty).Trim();
        newPin = (newPin ?? string.Empty).Trim();

        if (!IsValidPin(newPin)) 
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Invalid new PIN format: '{newPin}'");
            return false;
        }

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) 
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: User {userId} not found");
            return false;
        }

        System.Diagnostics.Debug.WriteLine($"PIN change debug: Changing PIN for user '{user.Name}' (ID: {user.Id})");

        // Debug: Verify current PIN
        var currentPinValid = PinHasher.VerifyPin(currentPin, user.PinSalt, user.PinHash);
        if (!currentPinValid) 
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Current PIN verification failed for user '{user.Name}'");
            return false;
        }

        // Debug: Hash new PIN
        var (salt, hash) = PinHasher.HashPin(newPin);
        
        // Debug: Verify new PIN hash immediately
        var newPinValidAfterHash = PinHasher.VerifyPin(newPin, salt, hash);
        if (!newPinValidAfterHash)
        {
            // This should never happen, but let's catch it
            System.Diagnostics.Debug.WriteLine("ERROR: New PIN hash verification failed immediately after hashing!");
            return false;
        }

        try
        {
            user.PinSalt = salt;
            user.PinHash = hash;
            var changes = await _db.SaveChangesAsync();
            
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Database save completed. Changes saved: {changes}");
            
            // Debug: Verify saved PIN can be validated
            var savedUser = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
            if (savedUser != null)
            {
                var savedPinValid = PinHasher.VerifyPin(newPin, savedUser.PinSalt, savedUser.PinHash);
                System.Diagnostics.Debug.WriteLine($"PIN change debug: Current PIN valid: {currentPinValid}, New PIN valid after save: {savedPinValid}");
                
                if (!savedPinValid)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: PIN verification failed after database save - this indicates a database issue");
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Database save failed: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"PIN change error for user '{user.Name}': {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResetUserPinTo9999Async(int targetUserId, int adminUserId, string adminPin)
    {
        adminPin = (adminPin ?? string.Empty).Trim();
        if (!IsValidPin(adminPin)) return false;

        var admin = await _db.Users.SingleOrDefaultAsync(x => x.Id == adminUserId);
        if (admin is null || !HasRole(admin, "Admin")) return false;
        if (!PinHasher.VerifyPin(adminPin, admin.PinSalt, admin.PinHash)) return false;

        var target = await _db.Users.SingleOrDefaultAsync(x => x.Id == targetUserId);
        if (target is null) return false;
        if (HasRole(target, "Admin") || string.Equals(target.Name, "Admin", StringComparison.Ordinal)) return false;

        var (salt, hash) = PinHasher.HashPin("9999");
        target.PinSalt = salt;
        target.PinHash = hash;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(int targetUserId, int adminUserId, string adminPin)
    {
        adminPin = (adminPin ?? string.Empty).Trim();
        if (!IsValidPin(adminPin)) return false;

        var admin = await _db.Users.SingleOrDefaultAsync(x => x.Id == adminUserId);
        if (admin is null || !HasRole(admin, "Admin")) return false;
        if (!PinHasher.VerifyPin(adminPin, admin.PinSalt, admin.PinHash)) return false;

        var target = await _db.Users.SingleOrDefaultAsync(x => x.Id == targetUserId);
        if (target is null) return false;
        if (HasRole(target, "Admin") || string.Equals(target.Name, "Admin", StringComparison.Ordinal)) return false;

        _db.Users.Remove(target);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TrySetIsLoggedInAsync(int userId)
    {
        await LoginStateGuard.WaitAsync();
        try
        {
            var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Users""
                SET ""IsLoggedIn"" = {true}
                WHERE ""Id"" = {userId}
                  AND ""IsLoggedIn"" = {false}");

            return rows > 0;
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task ClearIsLoggedInAsync(int userId)
    {
        await LoginStateGuard.WaitAsync();
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Users""
                SET ""IsLoggedIn"" = {false}
                WHERE ""Id"" = {userId}");
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task<int> ClearAllIsLoggedInAsync()
    {
        await LoginStateGuard.WaitAsync();
        try
        {
            var clearedUsers = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Users""
                SET ""IsLoggedIn"" = {false}
                WHERE ""IsLoggedIn"" = {true}");

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""LoginContexts""
                SET ""IsActive"" = {false}
                WHERE ""IsActive"" = {true}");

            return clearedUsers;
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task<bool> IsUserLoggedInAsync(int userId)
    {
        await LoginStateGuard.WaitAsync();
        try
        {
            var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
            return user?.IsLoggedIn == true;
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task<bool> IsSessionValidAsync(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirst("uid")?.Value;
        if (!int.TryParse(rawUserId, out var userId) || userId <= 0) return false;

        return await IsUserLoggedInAsync(userId);
    }

    public async Task<bool> IsContextValidAsync(string contextId, bool touch)
    {
        contextId = (contextId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(contextId)) return false;

        await LoginStateGuard.WaitAsync();
        try
        {
            var ctx = await _db.LoginContexts
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.ContextId == contextId && x.IsActive);

            if (ctx?.User is null) return false;
            if (!ctx.User.IsLoggedIn) return false;

            if (touch)
            {
                ctx.LastSeenUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return true;
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task<DbContextLoginResult> TryDbContextLoginAsync(string name, string pin)
    {
        name = (name ?? string.Empty).Trim();
        pin = (pin ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name) || !IsValidPin(pin))
        {
            return new DbContextLoginResult(false, false, true, null, null, "Invalid credentials");
        }

        var lookup = name.ToUpperInvariant();

        await LoginStateGuard.WaitAsync();
        try
        {
            var user = await _db.Users.SingleOrDefaultAsync(x => x.Name.ToUpper() == lookup);
            if (user is null)
            {
                return new DbContextLoginResult(false, false, true, null, null, "Invalid credentials");
            }

            if (!PinHasher.VerifyPin(pin, user.PinSalt, user.PinHash))
            {
                return new DbContextLoginResult(false, false, true, null, null, "Invalid credentials");
            }

            if (user.IsLoggedIn)
            {
                var hasActiveContext = await _db.LoginContexts
                    .AnyAsync(x => x.UserId == user.Id && x.IsActive);

                if (!hasActiveContext)
                {
                    // Repair stale lock state (e.g., old tab/browser closed without logout)
                    user.IsLoggedIn = false;
                    await _db.SaveChangesAsync();
                }
                else
                {
                    return new DbContextLoginResult(false, true, false, null, null, "Already logged in");
                }
            }

            user.IsLoggedIn = true;
            var contextId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;
            _db.LoginContexts.Add(new LoginContext
            {
                ContextId = contextId,
                UserId = user.Id,
                IsActive = true,
                CreatedUtc = now,
                LastSeenUtc = now
            });

            await _db.SaveChangesAsync();
            _ = _appLog.LogPlayerActionAsync(LogType.PlayerLogin, user.Id, $"Player {user.Name} logged in");
            return new DbContextLoginResult(true, false, false, contextId, user, null);
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task<AppUser?> GetUserByContextIdAsync(string contextId, bool touch)
    {
        contextId = (contextId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(contextId)) return null;

        await LoginStateGuard.WaitAsync();
        try
        {
            var ctx = await _db.LoginContexts
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.ContextId == contextId && x.IsActive);

            if (ctx?.User is null) return null;
            if (!ctx.User.IsLoggedIn)
            {
                ctx.IsActive = false;
                await _db.SaveChangesAsync();
                return null;
            }

            if (touch)
            {
                ctx.LastSeenUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return ctx.User;
        }
        finally
        {
            LoginStateGuard.Release();
        }
    }

    public async Task LogoutByContextIdAsync(string contextId, bool isTimeout = false)
    {
        contextId = (contextId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(contextId)) return;

        var shouldLog = false;
        var logUserId = 0;
        var logUserName = string.Empty;

        await LoginStateGuard.WaitAsync();
        try
        {
            var ctx = await _db.LoginContexts
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.ContextId == contextId && x.IsActive);

            if (ctx?.User is null) return;

            ctx.IsActive = false;
            ctx.LastSeenUtc = DateTime.UtcNow;

            var hasOtherActive = await _db.LoginContexts
                .AnyAsync(x => x.UserId == ctx.UserId && x.IsActive && x.ContextId != contextId);

            if (!hasOtherActive)
            {
                ctx.User.IsLoggedIn = false;
            }

            await _db.SaveChangesAsync();

            shouldLog = true;
            logUserId = ctx.User.Id;
            logUserName = ctx.User.Name ?? string.Empty;
        }
        finally
        {
            LoginStateGuard.Release();
        }

        if (shouldLog && logUserId > 0)
        {
            var logType = isTimeout ? LogType.PlayerTimeoutLogout : LogType.PlayerLogout;
            var detail = isTimeout
                ? $"Player {logUserName} logged out (session timeout)"
                : $"Player {logUserName} logged out (user initiated)";

            _ = _appLog.LogPlayerActionAsync(logType, logUserId, detail);
        }
    }

    private static bool IsValidPin(string pin)
        => (pin.Length == 4 || pin.Length == 6) && pin.All(char.IsDigit);

    private static bool HasRole(AppUser user, string role)
    {
        var roles = (user.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    public static ClaimsPrincipal BuildPrincipal(AppUser user, string? activeSessionId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Name),
            new("uid", user.Id.ToString())
        };

        if (!string.IsNullOrWhiteSpace(activeSessionId))
        {
            claims.Add(new Claim("sid", activeSessionId));
        }

        var roles = (user.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "DbContext");
        var principal = new ClaimsPrincipal(identity);
        
        // Debug logging for Azure
        Console.WriteLine($"BuildPrincipal debug: Building principal for user '{user.Name}' (ID: {user.Id})");
        Console.WriteLine($"BuildPrincipal debug: Claims created:");
        foreach (var claim in claims)
        {
            Console.WriteLine($"  - {claim.Type}: {claim.Value}");
        }
        Console.WriteLine($"BuildPrincipal debug: Authentication type: {identity.AuthenticationType}");
        Console.WriteLine($"BuildPrincipal debug: IsAuthenticated: {principal.Identity?.IsAuthenticated}");
        Console.WriteLine($"BuildPrincipal debug: Principal name: {principal.Identity?.Name}");
        
        return principal;
    }

    public async Task<AppUser?> CreateUserAsync(string name, string pin, bool isAdmin, bool isPlayer)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (!IsValidPin(pin)) return null;

        name = name.Trim();
        var lookup = name.ToUpperInvariant();

        var exists = await _db.Users.AnyAsync(x => x.Name.ToUpper() == lookup);
        if (exists) return null;

        var roles = new List<string>();
        if (isPlayer) roles.Add("Player");
        if (isAdmin) roles.Add("Admin");

        var (salt, hash) = PinHasher.HashPin(pin);

        var user = new AppUser
        {
            Name = name,
            Roles = string.Join(',', roles),
            PinSalt = salt,
            PinHash = hash
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user;
    }
}
