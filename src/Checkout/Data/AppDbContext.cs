using Microsoft.EntityFrameworkCore;

namespace Checkout.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CheckoutAudit> CheckoutAudits => Set<CheckoutAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CheckoutAudit>(entity =>
        {
            entity.ToTable("checkout_audit");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(x => x.Total).HasColumnName("total");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.Error).HasColumnName("error");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => x.RequestId).HasDatabaseName("ix_checkout_audit_request_id");
        });
    }
}
