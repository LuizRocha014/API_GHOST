using Application.Agenda;
using Domain.Agenda;
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
    public DbSet<AgendaSettings> AgendaSettings => Set<AgendaSettings>();
    public DbSet<AgendaBooking> AgendaBookings => Set<AgendaBooking>();
    public DbSet<AgendaAuditEntry> AgendaAudit => Set<AgendaAuditEntry>();
    public DbSet<AgendaPlanNotice> AgendaPlanNotices => Set<AgendaPlanNotice>();
    public DbSet<AgendaWaitlistEntry> AgendaWaitlist => Set<AgendaWaitlistEntry>();
    public DbSet<AgendaBlock> AgendaBlocks => Set<AgendaBlock>();
    public DbSet<AgendaClient> AgendaClients => Set<AgendaClient>();
    public DbSet<AgendaClientVehicle> AgendaClientVehicles => Set<AgendaClientVehicle>();
    public DbSet<AgendaReminder> AgendaReminders => Set<AgendaReminder>();
    public DbSet<AgendaLoyaltyPlan> AgendaLoyaltyPlans => Set<AgendaLoyaltyPlan>();
    public DbSet<AgendaClientPlan> AgendaClientPlans => Set<AgendaClientPlan>();
    public DbSet<AgendaPlanPayment> AgendaPlanPayments => Set<AgendaPlanPayment>();
    public DbSet<BotSessionRecord> BotSessions => Set<BotSessionRecord>();
    public DbSet<BotProcessedMessage> BotProcessedMessages => Set<BotProcessedMessage>();
    public DbSet<BotMessage> BotMessages => Set<BotMessage>();
    public DbSet<BotQueueItem> BotQueueItems => Set<BotQueueItem>();

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
            e.Property(x => x.Role).HasMaxLength(16).HasDefaultValue(UserRoles.Owner);
            e.Property(x => x.LockedUntil).HasColumnType("datetime2");
            e.Property(x => x.LastLoginAt).HasColumnType("datetime2");
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

        ConfigureAgenda(modelBuilder);
    }

    /// <summary>
    /// Tabelas da agenda da estética. Os repositórios leem e gravam com Dapper; o EF só cria o esquema.
    /// Datas da agenda são locais (fuso da estética) em <c>date</c>; horários em minutos desde 00:00.
    /// </summary>
    private static void ConfigureAgenda(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgendaSettings>(e =>
        {
            e.ToTable("AgendaSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConfigJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => x.BranchId).IsUnique();
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaBooking>(e =>
        {
            e.ToTable("AgendaBookings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasColumnType("date");
            e.Property(x => x.BoxId).HasMaxLength(AgendaService.MaxIdLength);
            e.Property(x => x.ServiceId).HasMaxLength(AgendaService.MaxIdLength);
            e.Property(x => x.ServiceName).HasMaxLength(AgendaService.MaxNameLength);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.VehicleType).HasMaxLength(16);
            e.Property(x => x.Vehicle).HasMaxLength(AgendaService.MaxVehicleLength);
            e.Property(x => x.Plate).HasMaxLength(16);
            e.Property(x => x.ClientName).HasMaxLength(AgendaService.MaxNameLength);
            e.Property(x => x.Phone).HasMaxLength(AgendaService.MaxPhoneLength);
            e.Property(x => x.Notes).HasMaxLength(AgendaService.MaxNotesLength);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Origin).HasMaxLength(16);
            e.Property(x => x.ExtrasJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.PaymentMethod).HasMaxLength(16);
            e.Property(x => x.PaidAmount).HasPrecision(18, 2);
            e.Property(x => x.PaidAt).HasColumnType("datetime2");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => x.SeriesId).HasFilter("[SeriesId] IS NOT NULL");
            e.Property(x => x.RatedAt).HasColumnType("datetime2");
            // A reserva trava os agendamentos do dia da filial (UPDLOCK, HOLDLOCK): o índice faz o lock pegar só o dia.
            e.HasIndex(x => new { x.BranchId, x.Date });
            e.HasIndex(x => new { x.BranchId, x.ClientId });
            // Uso do plano no mês: conta os agendamentos do plano por serviço e período.
            e.HasIndex(x => new { x.ClientPlanId, x.Date });
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaPlanNotice>(e =>
        {
            e.ToTable("AgendaPlanNotices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Month).HasColumnType("date");
            e.Property(x => x.Type).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.WamId).HasMaxLength(128);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.ClientPlanId, x.Month, x.Type }).IsUnique();
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaWaitlistEntry>(e =>
        {
            e.ToTable("AgendaWaitlist");
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasColumnType("date");
            e.Property(x => x.ServiceId).HasMaxLength(64);
            e.Property(x => x.VehicleType).HasMaxLength(32);
            e.Property(x => x.ExtraIds).HasMaxLength(500);
            e.Property(x => x.ClientName).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Origin).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.WamId).HasMaxLength(128);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.NotifiedAt).HasColumnType("datetime2");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.Date, x.Status });
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaAuditEntry>(e =>
        {
            e.ToTable("AgendaAudit");
            e.HasKey(x => x.Id);
            e.Property(x => x.Actor).HasMaxLength(120);
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.Details).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.BookingId, x.CreatedAt });
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaBlock>(e =>
        {
            e.ToTable("AgendaBlocks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasColumnType("date");
            e.Property(x => x.BoxId).HasMaxLength(AgendaService.MaxIdLength);
            e.Property(x => x.Reason).HasMaxLength(AgendaService.MaxReasonLength);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.Date });
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaClient>(e =>
        {
            e.ToTable("AgendaClients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Phone).HasMaxLength(AgendaService.MaxPhoneLength);
            e.Property(x => x.Name).HasMaxLength(AgendaService.MaxNameLength);
            e.Property(x => x.OptInAt).HasColumnType("datetime2");
            e.Property(x => x.Notes).HasMaxLength(AgendaService.MaxNotesLength);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.Phone });
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaClientVehicle>(e =>
        {
            e.ToTable("AgendaClientVehicles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Vehicle).HasMaxLength(AgendaService.MaxVehicleLength);
            e.Property(x => x.Plate).HasMaxLength(16);
            e.Property(x => x.VehicleType).HasMaxLength(16);
            e.Property(x => x.Color).HasMaxLength(AgendaService.MaxColorLength);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.ClientId });
            e.HasOne<AgendaClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaReminder>(e =>
        {
            e.ToTable("AgendaReminders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.ScheduledFor).HasColumnType("datetime2");
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.WamId).HasMaxLength(128);
            e.Property(x => x.Answer).HasMaxLength(16);
            e.Property(x => x.AnsweredAt).HasColumnType("datetime2");
            e.Property(x => x.Error).HasMaxLength(AgendaService.MaxErrorLength);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.Status, x.ScheduledFor });
            e.HasIndex(x => x.BookingId);
            e.HasIndex(x => x.WamId);
            e.HasOne<AgendaBooking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgendaLoyaltyPlan>(e =>
        {
            e.ToTable("AgendaLoyaltyPlans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(AgendaService.MaxNameLength);
            e.Property(x => x.Description).HasMaxLength(AgendaService.MaxNotesLength);
            e.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.ItemsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => x.BranchId);
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaClientPlan>(e =>
        {
            e.ToTable("AgendaClientPlans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(AgendaService.MaxNameLength);
            e.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.ItemsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.VehicleType).HasMaxLength(16);
            e.Property(x => x.FullPrice).HasPrecision(18, 2);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.StartDate).HasColumnType("date");
            e.Property(x => x.EndDate).HasColumnType("date");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.ClientId });
            e.HasIndex(x => x.PlanId);
            e.HasOne<AgendaClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AgendaLoyaltyPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgendaPlanPayment>(e =>
        {
            e.ToTable("AgendaPlanPayments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Month).HasColumnType("date");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.PaidAt).HasColumnType("datetime2");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            // Um pagamento por mês e plano; desfeito (Active = 0) libera o mês para pagar de novo.
            e.HasIndex(x => new { x.ClientPlanId, x.Month }).IsUnique().HasFilter("[Active] = 1");
            e.HasIndex(x => new { x.BranchId, x.ClientId });
            e.HasOne<AgendaClientPlan>().WithMany().HasForeignKey(x => x.ClientPlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BotSessionRecord>(e =>
        {
            e.ToTable("BotSessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Phone).HasMaxLength(AgendaService.MaxPhoneLength);
            e.Property(x => x.State).HasMaxLength(32);
            e.Property(x => x.DataJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.SeenAt).HasColumnType("datetime2");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.Phone }).IsUnique();
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BotProcessedMessage>(e =>
        {
            e.ToTable("BotProcessedMessages");
            e.HasKey(x => x.MessageId);
            e.Property(x => x.MessageId).HasMaxLength(128);
            e.Property(x => x.ReceivedAt).HasColumnType("datetime2");
            e.HasIndex(x => x.ReceivedAt);
        });

        modelBuilder.Entity<BotQueueItem>(e =>
        {
            e.ToTable("BotQueueItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.ExternalId).HasMaxLength(160);
            e.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.ReceivedAt).HasColumnType("datetime2");
            e.Property(x => x.NextAttemptAt).HasColumnType("datetime2");
            e.Property(x => x.ProcessedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasIndex(x => x.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
        });

        modelBuilder.Entity<BotMessage>(e =>
        {
            e.ToTable("BotMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Phone).HasMaxLength(AgendaService.MaxPhoneLength);
            e.Property(x => x.Direction).HasMaxLength(8);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Body).HasMaxLength(4096);
            e.Property(x => x.WamId).HasMaxLength(128);
            e.Property(x => x.Meta).HasColumnType("nvarchar(max)");
            e.Property(x => x.Author).HasMaxLength(80);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            e.HasIndex(x => new { x.BranchId, x.Phone, x.CreatedAt });
            e.HasIndex(x => x.CreatedAt);
        });
    }
}
