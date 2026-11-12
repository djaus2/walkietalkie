using Microsoft.EntityFrameworkCore;

namespace WalkieTalkie.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<UsableMsVoice> UsableMsVoices => Set<UsableMsVoice>();
    public DbSet<AppLog> AppLogs => Set<AppLog>();
    public DbSet<LoginContext> LoginContexts => Set<LoginContext>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
            b.Property(x => x.IsLoggedIn).HasDefaultValue(false).IsRequired();
        });

        modelBuilder.Entity<UsableMsVoice>(b =>
        {
            b.HasIndex(x => new { x.BrowserFamily, x.Token }).IsUnique();
        });

        modelBuilder.Entity<LoginContext>(b =>
        {
            b.HasIndex(x => x.ContextId).IsUnique();
            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
