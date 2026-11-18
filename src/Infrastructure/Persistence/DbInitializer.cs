using Application.Abstractions;
using Application.Auth;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Dados iniciais. <see cref="DemoData"/> = empresa, filiais e produtos de demonstração (desenvolvimento); sem ele, uma empresa
/// e uma filial com os nomes dados. <see cref="ForceSeedPasswordChange"/> marca a troca obrigatória para quem ainda usa a senha
/// pública do seed.
/// </summary>
public sealed record SeedOptions(bool DemoData, string CompanyName, string BranchName, bool ForceSeedPasswordChange);

public static class DbInitializer
{
    /// <summary>Semeia o banco vazio. Devolve quantos usuários passaram a ter que trocar a senha padrão.</summary>
    public static async Task<int> SeedAsync(
        AppDbContext db, IPasswordHasher passwordHasher, SeedOptions options, CancellationToken cancellationToken = default)
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

        if (!await db.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            if (options.DemoData)
                SeedDemo(db, passwordHasher);
            else
                SeedEmpty(db, passwordHasher, options);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return options.ForceSeedPasswordChange ? await ForceSeedPasswordChangeAsync(db, passwordHasher, cancellationToken) : 0;
    }

    private static async Task<int> ForceSeedPasswordChangeAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        var candidates = await db.Users.Where(u => u.Active && !u.MustChangePassword).ToListAsync(ct).ConfigureAwait(false);
        var forced = 0;
        foreach (var user in candidates.Where(u => hasher.Verify(PasswordPolicy.SeedPassword, u.PasswordHash)))
        {
            user.MustChangePassword = true;
            user.UpdatedAt = DateTime.UtcNow;
            forced++;
        }
        if (forced > 0) await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return forced;
    }

    private static User Admin(IPasswordHasher hasher, DateTime utc, bool mustChange) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Administrador",
        Username = "admin",
        Email = "admin@example.com",
        PasswordHash = hasher.Hash(PasswordPolicy.SeedPassword),
        Role = UserRoles.Owner,
        MustChangePassword = mustChange,
        CreatedAt = utc,
        UpdatedAt = utc,
        Active = true
    };

    /// <summary>Produção: só a empresa, uma filial e o dono (que troca a senha no primeiro acesso).</summary>
    private static void SeedEmpty(AppDbContext db, IPasswordHasher hasher, SeedOptions options)
    {
        var utc = DateTime.UtcNow;
        var companyId = Guid.NewGuid();
        db.Companies.Add(new Company { Id = companyId, Name = options.CompanyName, Cnpj = string.Empty, CreatedAt = utc, UpdatedAt = utc, Active = true });
        db.Branches.Add(new Branch { Id = Guid.NewGuid(), CompanyId = companyId, Name = options.BranchName, CreatedAt = utc, UpdatedAt = utc, Active = true });
        db.Users.Add(Admin(hasher, utc, mustChange: true));
    }

    private static void SeedDemo(AppDbContext db, IPasswordHasher passwordHasher)
    {
        var utc = DateTime.UtcNow;
        var companyId = Guid.NewGuid();
        var branchCentroId = Guid.NewGuid();
        var branchSulId = Guid.NewGuid();

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

        db.Users.Add(Admin(passwordHasher, utc, mustChange: false));

        db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
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
                Id = Guid.NewGuid(),
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
                Id = Guid.NewGuid(),
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
    }
}
