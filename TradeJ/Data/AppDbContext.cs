using Microsoft.EntityFrameworkCore;
using TradeJ.Models;

namespace TradeJ.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<TagCategory> TagCategories => Set<TagCategory>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TradeTag> TradeTags => Set<TradeTag>();
    public DbSet<DayTagDef> DayTagDefs => Set<DayTagDef>();
    public DbSet<DayTag> DayTags => Set<DayTag>();
    public DbSet<DayNote> DayNotes => Set<DayNote>();
    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<TradeStrategy> TradeStrategies => Set<TradeStrategy>();
    public DbSet<StrategyNote> StrategyNotes => Set<StrategyNote>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasOne(t => t.Account)
                .WithMany(a => a.Trades)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => new { t.AccountId, t.BrokerTradeId }).IsUnique();

            entity.Property(t => t.EntryPrice).HasPrecision(18, 8);
            entity.Property(t => t.ExitPrice).HasPrecision(18, 8);
            entity.Property(t => t.Volume).HasPrecision(18, 8);
            entity.Property(t => t.GrossPnL).HasPrecision(18, 2);
            entity.Property(t => t.Commission).HasPrecision(18, 2);
            entity.Property(t => t.Swap).HasPrecision(18, 2);
            entity.Property(t => t.NetPnL).HasPrecision(18, 2);
        });



        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasOne(t => t.Category)
                .WithMany(c => c.Tags)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TradeTag>(entity =>
        {
            entity.HasOne(tt => tt.Trade)
                .WithMany(t => t.TradeTags)
                .HasForeignKey(tt => tt.TradeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(tt => tt.Tag)
                .WithMany(t => t.TradeTags)
                .HasForeignKey(tt => tt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DayNote>(entity =>
        {
            entity.HasIndex(n => n.Date).IsUnique();
        });

        modelBuilder.Entity<DayTag>(entity =>
        {
            entity.HasOne(dt => dt.DayTagDef)
                .WithMany(d => d.DayTags)
                .HasForeignKey(dt => dt.DayTagDefId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(dt => new { dt.Date, dt.DayTagDefId }).IsUnique();
        });

        modelBuilder.Entity<TradeStrategy>(entity =>
        {
            entity.HasKey(ts => new { ts.TradeId, ts.StrategyId });
            entity.HasOne(ts => ts.Trade)
                .WithMany(t => t.TradeStrategies)
                .HasForeignKey(ts => ts.TradeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ts => ts.Strategy)
                .WithMany(s => s.TradeStrategies)
                .HasForeignKey(ts => ts.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StrategyNote>(entity =>
        {
            entity.HasOne(n => n.Strategy)
                .WithMany(s => s.Notes)
                .HasForeignKey(n => n.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(s => s.Key);
        });
    }
}
