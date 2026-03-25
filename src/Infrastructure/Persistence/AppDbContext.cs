using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockMovementItem> StockMovementItems => Set<StockMovementItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Production> Productions => Set<Production>();
    public DbSet<ProductionItem> ProductionItems => Set<ProductionItem>();
    public DbSet<Access> Accesses => Set<Access>();
    public DbSet<UserCompanyBranch> UserCompanyBranches => Set<UserCompanyBranch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().ToTable("Companies");
        modelBuilder.Entity<Branch>().ToTable("Branches");
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<Product>().ToTable("Products");
        modelBuilder.Entity<ProductImage>().ToTable("ProductImages");
        modelBuilder.Entity<Sale>().ToTable("Sales");
        modelBuilder.Entity<Production>().ToTable("Productions");
        modelBuilder.Entity<StockMovement>().ToTable("StockMovements");
        modelBuilder.Entity<StockMovementItem>().ToTable("StockMovementItems");
        modelBuilder.Entity<SaleItem>().ToTable("SaleItems");
        modelBuilder.Entity<ProductionItem>().ToTable("ProductionItems");

        modelBuilder.Entity<Company>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Cnpj).HasMaxLength(32);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        });

        modelBuilder.Entity<Branch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Username).HasMaxLength(128);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PasswordHash).HasMaxLength(512);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Sku).HasMaxLength(64);
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.Property(x => x.UnitType).HasMaxLength(8);
            e.Property(x => x.SalePrice).HasPrecision(18, 4);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => x.Sku).IsUnique();
        });

        modelBuilder.Entity<ProductImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Url).HasMaxLength(2048);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductBatch>(e =>
        {
            e.ToTable("ProductBatches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.InitialQuantity).HasPrecision(18, 4);
            e.Property(x => x.CostPrice).HasPrecision(18, 4);
            e.Property(x => x.ExpirationDate).HasColumnType("datetime2");
            e.Property(x => x.EntryDate).HasColumnType("datetime2");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchDestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovementItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.CostPrice).HasPrecision(18, 4);
            e.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.MovementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProductBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Total).HasPrecision(18, 4);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SaleItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.Price).HasPrecision(18, 4);
            e.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProductBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Production>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Access>(e =>
        {
            e.ToTable("Accesses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => x.Code);
        });

        modelBuilder.Entity<UserCompanyBranch>(e =>
        {
            e.ToTable("UserCompanyBranches");
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.UserId, x.BranchId, x.AccessId }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Access>().WithMany().HasForeignKey(x => x.AccessId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.QuantityInput).HasPrecision(18, 4);
            e.Property(x => x.QuantityOutput).HasPrecision(18, 4);
            e.HasOne<Production>().WithMany().HasForeignKey(x => x.ProductionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductInputId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductOutputId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProductBatch>()
                .WithMany()
                .HasForeignKey(x => x.BatchInputId)
                .HasConstraintName("FK_ProductionItems_BatchInput")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProductBatch>()
                .WithMany()
                .HasForeignKey(x => x.OutputBatchId)
                .HasConstraintName("FK_ProductionItems_OutputBatch")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
