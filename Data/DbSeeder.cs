using WalkieTalkie.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalkieTalkie.Data;

public static class DbSeeder
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        Console.WriteLine("DbSeeder: Starting database seeding...");
        var created = await db.Database.EnsureCreatedAsync();
        Console.WriteLine($"DbSeeder: EnsureCreatedAsync returned: {created}");
        Console.WriteLine($"DbSeeder: Database connection: {db.Database.GetDbConnection().ConnectionString}");

        if (created)
        {
            Console.WriteLine("DbSeeder: Database was newly created, seeding initial data...");
            var settings = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1);
            if (settings is null)
            {
                Console.WriteLine("DbSeeder: Creating default settings...");
                db.Settings.Add(new AppSettings
                {
                    Id = 1,
                    MaxLoginHrs = 4,
                    AllowPlayerPinChange = true
                });
            }
        }
        else
        {
            Console.WriteLine("DbSeeder: Database already existed, checking for new tables...");
        }

        // Create Groups table if it doesn't exist
        if (!await TableExistsAsync(db, "Groups"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE ""Groups"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""OwnerUserId"" INTEGER NOT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    FOREIGN KEY (""OwnerUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
                )");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_Groups_Name\" ON \"Groups\" (\"Name\")");
        }

        // Create GroupMembers table if it doesn't exist
        if (!await TableExistsAsync(db, "GroupMembers"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE ""GroupMembers"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""GroupId"" INTEGER NOT NULL,
                    ""UserId"" INTEGER NOT NULL,
                    ""JoinedAtUtc"" TEXT NOT NULL,
                    FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE CASCADE,
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                )");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_GroupMembers_GroupId_UserId\" ON \"GroupMembers\" (\"GroupId\", \"UserId\")");
        }

        // Create UsableMsVoices table if it doesn't exist
        if (!await TableExistsAsync(db, "UsableMsVoices"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE ""UsableMsVoices"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""BrowserFamily"" TEXT NOT NULL,
                    ""Token"" TEXT NOT NULL,
                    ""VoiceName"" TEXT NOT NULL,
                    ""VoiceLang"" TEXT NULL,
                    ""VoiceUri"" TEXT NULL,
                    ""UpdatedUtc"" TEXT NOT NULL
                )");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_UsableMsVoices_BrowserFamily_Token\" ON \"UsableMsVoices\" (\"BrowserFamily\", \"Token\")");
        }

        // Create AppLogs table if it doesn't exist
        try
        {
            // Try to query the table - if it fails, it doesn't exist
            await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM [AppLogs] LIMIT 1");
            Console.WriteLine("AppLogs table already exists");
        }
        catch
        {
            // Table doesn't exist, create it
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE [AppLogs] (
                        [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                        [Timestamp] TEXT NOT NULL,
                        [LogType] INTEGER NOT NULL,
                        [PlayerId] INTEGER NOT NULL,
                        [GroupId] INTEGER NULL,
                        [Details] TEXT NULL
                    )");
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX [IX_AppLogs_Timestamp] ON [AppLogs] ([Timestamp] ASC)");
                Console.WriteLine("AppLogs table created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating AppLogs table: {ex.Message}");
            }
        }

        if (!await UserColumnExistsAsync(db, "AdminDesktopFallbackTtsVoice"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN \"AdminDesktopFallbackTtsVoice\" TEXT NULL");
        }

        if (!await UserColumnExistsAsync(db, "AdminDesktopFallbackTtsLanguage"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN \"AdminDesktopFallbackTtsLanguage\" TEXT NULL");
        }

        if (!await UserColumnExistsAsync(db, "AdminDesktopFallbackTtsRegion"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN \"AdminDesktopFallbackTtsRegion\" TEXT NULL");
        }

        if (!await UserColumnExistsAsync(db, "ActiveSessionId"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN \"ActiveSessionId\" TEXT NULL");
        }

        if (!await UserColumnExistsAsync(db, "IsLoggedIn"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN \"IsLoggedIn\" INTEGER NOT NULL DEFAULT 0");
        }

        if (!await TableExistsAsync(db, "LoginContexts"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE ""LoginContexts"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""ContextId"" TEXT NOT NULL,
                    ""UserId"" INTEGER NOT NULL,
                    ""IsActive"" INTEGER NOT NULL,
                    ""CreatedUtc"" TEXT NOT NULL,
                    ""LastSeenUtc"" TEXT NOT NULL,
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                )");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_LoginContexts_ContextId\" ON \"LoginContexts\" (\"ContextId\")");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX \"IX_LoginContexts_UserId_IsActive\" ON \"LoginContexts\" (\"UserId\", \"IsActive\")");
        }

        await db.SaveChangesAsync();

        // Keep DB aligned with auth.json on every startup (groups first, then users)
        await CreateGroupsAndMembershipsFromAuthJson(db);
        await CreateUsersFromAuthJson(db);

        await db.SaveChangesAsync();
    }

    public static async Task ValidateSchemaHealthAsync(AppDbContext db)
    {
        var issues = new List<string>();

        async Task RequireTable(string table)
        {
            if (!await TableExistsAsync(db, table))
            {
                issues.Add($"Missing table: {table}");
            }
        }

        async Task RequireColumn(string table, string column)
        {
            if (!await ColumnExistsAsync(db, table, column))
            {
                issues.Add($"Missing column: {table}.{column}");
            }
        }

        // Core tables
        await RequireTable("Users");
        await RequireTable("Settings");
        await RequireTable("Groups");
        await RequireTable("GroupMembers");
        await RequireTable("UsableMsVoices");
        await RequireTable("AppLogs");
        await RequireTable("LoginContexts");

        // Core user/auth columns
        await RequireColumn("Users", "Name");
        await RequireColumn("Users", "Roles");
        await RequireColumn("Users", "PinSalt");
        await RequireColumn("Users", "PinHash");
        await RequireColumn("Users", "IsLoggedIn");

        // DB-context auth columns
        await RequireColumn("LoginContexts", "ContextId");
        await RequireColumn("LoginContexts", "UserId");
        await RequireColumn("LoginContexts", "IsActive");
        await RequireColumn("LoginContexts", "CreatedUtc");
        await RequireColumn("LoginContexts", "LastSeenUtc");

        if (issues.Count > 0)
        {
            var message = "Database schema health check failed:\n - " + string.Join("\n - ", issues);
            throw new InvalidOperationException(message);
        }
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableName)
    {
        var count = await ExecuteScalarIntAsync(
            db,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name",
            "@name",
            tableName);

        return count > 0;
    }

    private static async Task<bool> UserColumnExistsAsync(AppDbContext db, string columnName)
    {
        var count = await ExecuteScalarIntAsync(
            db,
            "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name=@name",
            "@name",
            columnName);

        return count > 0;
    }

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string tableName, string columnName)
    {
        var safeTable = tableName.Replace("'", "''", StringComparison.Ordinal);
        var count = await ExecuteScalarIntAsync(
            db,
            $"SELECT COUNT(*) FROM pragma_table_info('{safeTable}') WHERE name=@name",
            "@name",
            columnName);

        return count > 0;
    }

    private static async Task<int> ExecuteScalarIntAsync(
        AppDbContext db,
        string sql,
        string parameterName,
        string parameterValue)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = parameterValue;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task CreateUsersFromAuthJson(AppDbContext db)
    {
        var authJsonPath = ResolveAuthJsonPath();
        Console.WriteLine($"DbSeeder: Resolved auth.json path: {authJsonPath ?? "<not found>"}");

        if (string.IsNullOrWhiteSpace(authJsonPath) || !File.Exists(authJsonPath))
        {
            Console.WriteLine("Warning: auth.json not found, skipping user creation from JSON");
            return;
        }

        var json = await File.ReadAllTextAsync(authJsonPath);
        Console.WriteLine($"DbSeeder: auth.json loaded successfully, length: {json.Length}");
        var authExport = JsonSerializer.Deserialize<AuthExport>(json);
        
        if (authExport?.Users == null)
        {
            Console.WriteLine("Warning: No users found in auth.json");
            return;
        }

        Console.WriteLine($"DbSeeder: Found {authExport.Users.Count} users in auth.json");
        var usersCreated = 0;
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var authUser in authExport.Users)
        {
            var userName = (authUser.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                Console.WriteLine("DbSeeder: Skipping user with blank name in auth.json");
                continue;
            }

            if (!seenNames.Add(userName))
            {
                Console.WriteLine($"DbSeeder: Duplicate user name '{userName}' in auth.json, skipping duplicate entry");
                continue;
            }

            var existingInDb = await db.Users.AnyAsync(x => x.Name == userName);
            var existingInLocal = db.Users.Local.Any(x => string.Equals(x.Name, userName, StringComparison.OrdinalIgnoreCase));

            if (!existingInDb && !existingInLocal)
            {
                Console.WriteLine($"DbSeeder: Creating user: {userName}");
                var (salt, hash) = PinHasher.HashPin(authUser.Pin);
                db.Users.Add(new AppUser
                {
                    Name = userName,
                    Roles = authUser.Roles,
                    PinSalt = salt,
                    PinHash = hash
                });
                usersCreated++;
            }
            else
            {
                Console.WriteLine($"DbSeeder: User {userName} already exists");
            }
        }
        
        Console.WriteLine($"DbSeeder: Created {usersCreated} new users");
    }

    private static async Task CreateGroupsAndMembershipsFromAuthJson(AppDbContext db)
    {
        var authJsonPath = ResolveAuthJsonPath();

        if (string.IsNullOrWhiteSpace(authJsonPath) || !File.Exists(authJsonPath))
        {
            Console.WriteLine("Warning: auth.json not found, skipping group creation from JSON");
            return;
        }

        var json = await File.ReadAllTextAsync(authJsonPath);
        var authExport = JsonSerializer.Deserialize<AuthExport>(json);
        
        if (authExport?.Groups == null || authExport.Users == null)
        {
            Console.WriteLine("Warning: Missing users/groups in auth.json");
            return;
        }

        Console.WriteLine($"DbSeeder: Processing {authExport.Groups.Count} groups from auth.json");

        // Create groups and memberships
        foreach (var authGroup in authExport.Groups)
        {
            var existingGroup = await db.Groups.FirstOrDefaultAsync(x => x.Name == authGroup.Name);
            Group? group = null;
            
            if (existingGroup is null)
            {
                // Find the owner user by name (lookup outside of EF query)
                var ownerUserAuth = authExport.Users.FirstOrDefault(u => u.Id == authGroup.OwnerId);
                if (ownerUserAuth != null)
                {
                    var ownerUser = await db.Users.SingleOrDefaultAsync(x => x.Name == ownerUserAuth.Name);
                    if (ownerUser == null)
                    {
                        var (ownerSalt, ownerHash) = PinHasher.HashPin(ownerUserAuth.Pin);
                        ownerUser = new AppUser
                        {
                            Name = ownerUserAuth.Name,
                            Roles = ownerUserAuth.Roles,
                            PinSalt = ownerSalt,
                            PinHash = ownerHash
                        };
                        db.Users.Add(ownerUser);
                        await db.SaveChangesAsync();
                        Console.WriteLine($"DbSeeder: Created missing owner user '{ownerUser.Name}' for group '{authGroup.Name}'");
                    }

                    if (ownerUser != null)
                    {
                        group = new Group
                        {
                            Name = authGroup.Name,
                            Description = authGroup.Description,
                            OwnerUserId = ownerUser.Id,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        db.Groups.Add(group);
                        await db.SaveChangesAsync(); // Save to get the group ID
                        Console.WriteLine($"DbSeeder: Created new group '{group.Name}' with ID {group.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"DbSeeder: Owner user '{ownerUserAuth.Name}' unavailable for group '{authGroup.Name}'");
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine($"DbSeeder: Owner user with ID {authGroup.OwnerId} not found for group '{authGroup.Name}'");
                    continue;
                }
            }
            else
            {
                group = existingGroup;
                Console.WriteLine($"DbSeeder: Using existing group '{group.Name}' with ID {group.Id}");
            }

            if (group is null)
            {
                continue;
            }

            foreach (var memberName in authGroup.Members)
            {
                var safeMemberName = (memberName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(safeMemberName))
                {
                    continue;
                }

                var memberUser = await db.Users.SingleOrDefaultAsync(x => x.Name == safeMemberName);
                if (memberUser != null)
                {
                    var exists = await db.GroupMembers.AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == memberUser.Id);
                    if (!exists)
                    {
                        db.GroupMembers.Add(new GroupMember
                        {
                            GroupId = group.Id,
                            UserId = memberUser.Id,
                            JoinedAtUtc = DateTime.UtcNow
                        });
                        Console.WriteLine($"DbSeeder: Added member '{safeMemberName}' (ID: {memberUser.Id}) to group '{group.Name}'");
                    }
                }
                else
                {
                    // Groups-first requirement: create missing users encountered while processing groups
                    var memberAuthUser = authExport.Users.FirstOrDefault(u =>
                        string.Equals(u.Name, safeMemberName, StringComparison.OrdinalIgnoreCase));

                    if (memberAuthUser != null)
                    {
                        var (salt, hash) = PinHasher.HashPin(memberAuthUser.Pin);
                        memberUser = new AppUser
                        {
                            Name = safeMemberName,
                            Roles = memberAuthUser.Roles,
                            PinSalt = salt,
                            PinHash = hash
                        };
                        db.Users.Add(memberUser);
                        await db.SaveChangesAsync();

                        db.GroupMembers.Add(new GroupMember
                        {
                            GroupId = group.Id,
                            UserId = memberUser.Id,
                            JoinedAtUtc = DateTime.UtcNow
                        });
                        Console.WriteLine($"DbSeeder: Created missing user '{safeMemberName}' and added to group '{group.Name}'");
                    }
                    else
                    {
                        Console.WriteLine($"DbSeeder: Member user '{safeMemberName}' not found in auth.json for group '{authGroup.Name}'");
                    }
                }
            }
        }
        
        await db.SaveChangesAsync();
        Console.WriteLine("DbSeeder: Group and membership creation completed");
    }

    private static string? ResolveAuthJsonPath()
    {
        var candidates = new List<string>();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();

        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            candidates.Add(Path.Combine(baseDir, "auth.json"));
        }

        if (!string.IsNullOrWhiteSpace(currentDir))
        {
            candidates.Add(Path.Combine(currentDir, "auth.json"));
        }

        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            var dir = new DirectoryInfo(baseDir);
            for (var i = 0; i < 8 && dir is not null; i++)
            {
                candidates.Add(Path.Combine(dir.FullName, "auth.json"));
                dir = dir.Parent;
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in candidates)
        {
            if (!seen.Add(path)) continue;
            try
            {
                if (File.Exists(path)) return path;
            }
            catch
            {
            }
        }

        return null;
    }
}
