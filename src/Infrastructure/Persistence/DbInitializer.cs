using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        if (!await db.Accesses.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            var utcSeed = DateTime.UtcNow;
            db.Accesses.Add(new Access
            {
                Id = SeedIds.DefaultAccessId,
                Name = "Geral",
                Code = "GERAL",
                CreatedAt = utcSeed,
                UpdatedAt = utcSeed,
                Active = true
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (await db.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
            return;

        var utc = DateTime.UtcNow;
        var companyId = Guid.NewGuid();
        var branchCentroId = Guid.NewGuid();
        var branchSulId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productNotebookId = Guid.NewGuid();
        var productMouseId = Guid.NewGuid();
        var productMonitorId = Guid.NewGuid();

        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa Demo",
            Cnpj = "00000000000191",
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        });

        db.Branches.AddRange(
            new Branch
            {
                Id = branchCentroId,
                CompanyId = companyId,
                Name = "Filial Centro",
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            },
            new Branch
            {
                Id = branchSulId,
                CompanyId = companyId,
                Name = "Filial Zona Sul",
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            });

        db.Users.Add(new User
        {
            Id = userId,
            Name = "Administrador",
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = passwordHasher.Hash("Admin@123"),
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        });

        db.Products.AddRange(
            new Product
            {
                Id = productNotebookId,
                Name = "Notebook Pro 14",
                Sku = "NB-PRO-14",
                Barcode = "7891000123456",
                UnitType = "UN",
                IsPerishable = false,
                SalePrice = 7499.90m,
                CreatedAt = utc.AddDays(-30),
                UpdatedAt = utc.AddDays(-30),
                Active = true
            },
            new Product
            {
                Id = productMouseId,
                Name = "Mouse sem fio",
                Sku = "MS-WL-01",
                Barcode = null,
                UnitType = "UN",
                IsPerishable = false,
                SalePrice = 129.90m,
                CreatedAt = utc.AddDays(-10),
                UpdatedAt = utc.AddDays(-10),
                Active = true
            },
            new Product
            {
                Id = productMonitorId,
                Name = "Monitor 27\" 4K",
                Sku = "MN-27-4K",
                Barcode = null,
                UnitType = "UN",
                IsPerishable = false,
                SalePrice = 1899.00m,
                CreatedAt = utc.AddDays(-5),
                UpdatedAt = utc.AddDays(-5),
                Active = true
            });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
