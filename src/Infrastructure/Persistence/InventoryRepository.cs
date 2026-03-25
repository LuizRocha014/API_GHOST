using Application.Abstractions;
using Dapper;
using Domain;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly SqlSession _session;

    public InventoryRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<decimal> GetTotalStockForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COALESCE(SUM(Quantity), 0)
            FROM ProductBatches
            WHERE ProductId = @ProductId AND Active = 1
            """;
        return await _session.Connection
            .ExecuteScalarAsync<decimal>(new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductBatch>> GetBatchesForProductAndBranchAsync(
        Guid productId,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE ProductId = @ProductId AND BranchId = @BranchId AND Active = 1 AND Quantity > 0
            """;
        var list = await _session.Connection
            .QueryAsync<ProductBatch>(new CommandDefinition(sql, new { ProductId = productId, BranchId = branchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<IReadOnlyList<ProductBatch>> ListAllBatchesForProductAndBranchAsync(
        Guid productId,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE ProductId = @ProductId AND BranchId = @BranchId
            ORDER BY EntryDate
            """;
        var list = await _session.Connection
            .QueryAsync<ProductBatch>(new CommandDefinition(sql, new { ProductId = productId, BranchId = branchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE Id = @Id
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<ProductBatch>(new CommandDefinition(sql, new { Id = batchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<RegisterEntryResult> RegisterEntryAsync(
        RegisterEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Quantity <= 0)
            throw new InvalidOperationException("Quantidade de entrada deve ser maior que zero.");

        var utc = DateTime.UtcNow;
        var batchId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using var tx = await _session.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            const string insertBatch = """
                INSERT INTO ProductBatches (Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active)
                VALUES (@Id, @ProductId, @BranchId, @Quantity, @InitialQuantity, @CostPrice, @ExpirationDate, @EntryDate, @CreatedAt, @UpdatedAt, @Active)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertBatch, new
            {
                Id = batchId,
                command.ProductId,
                command.BranchId,
                Quantity = command.Quantity,
                InitialQuantity = command.Quantity,
                command.CostPrice,
                command.ExpirationDate,
                EntryDate = command.EntryDate,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertMovement = """
                INSERT INTO StockMovements (Id, Type, BranchId, BranchDestId, CreatedAt, CreatedBy)
                VALUES (@Id, @Type, @BranchId, NULL, @CreatedAt, @CreatedBy)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovement, new
            {
                Id = movementId,
                Type = (int)StockMovementType.Entry,
                command.BranchId,
                CreatedAt = utc,
                command.CreatedBy
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertItem = """
                INSERT INTO StockMovementItems (Id, MovementId, ProductId, BatchId, Quantity, CostPrice)
                VALUES (@Id, @MovementId, @ProductId, @BatchId, @Quantity, @CostPrice)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertItem, new
            {
                Id = itemId,
                MovementId = movementId,
                command.ProductId,
                BatchId = batchId,
                Quantity = command.Quantity,
                command.CostPrice
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var batch = new ProductBatch
        {
            Id = batchId,
            ProductId = command.ProductId,
            BranchId = command.BranchId,
            Quantity = command.Quantity,
            InitialQuantity = command.Quantity,
            CostPrice = command.CostPrice,
            ExpirationDate = command.ExpirationDate,
            EntryDate = command.EntryDate,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        return new RegisterEntryResult(batch, movementId);
    }

    public async Task<RegisterSaleResult> RegisterSaleAsync(
        RegisterSaleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
            throw new InvalidOperationException("A venda deve ter ao menos um item.");

        var batchIds = command.Lines.Select(l => l.BatchId).Distinct().ToArray();
        const string loadBatches = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE Id IN @Ids
            """;
        var batches = (await _session.Connection
                .QueryAsync<ProductBatch>(new CommandDefinition(loadBatches, new { Ids = batchIds }, cancellationToken: cancellationToken))
                .ConfigureAwait(false))
            .ToList();

        foreach (var line in command.Lines)
        {
            var batch = batches.FirstOrDefault(b => b.Id == line.BatchId);
            if (batch is null)
                throw new InvalidOperationException($"Lote {line.BatchId} não encontrado.");

            if (batch.BranchId != command.BranchId)
                throw new InvalidOperationException($"O lote {line.BatchId} não pertence à filial da venda.");

            if (batch.Quantity < line.Quantity)
                throw new InvalidOperationException(
                    $"Estoque insuficiente no lote {line.BatchId}. Disponível: {batch.Quantity}, solicitado: {line.Quantity}.");
        }

        var utc = DateTime.UtcNow;
        var saleId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var total = command.Lines.Sum(l => l.Quantity * l.UnitSalePrice);

        await using var tx = await _session.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            const string insertSale = """
                INSERT INTO Sales (Id, BranchId, Total, CreatedAt, UpdatedAt, Active)
                VALUES (@Id, @BranchId, @Total, @CreatedAt, @UpdatedAt, @Active)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertSale, new
            {
                Id = saleId,
                command.BranchId,
                Total = total,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertMovement = """
                INSERT INTO StockMovements (Id, Type, BranchId, BranchDestId, CreatedAt, CreatedBy)
                VALUES (@Id, @Type, @BranchId, NULL, @CreatedAt, @CreatedBy)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovement, new
            {
                Id = movementId,
                Type = (int)StockMovementType.Exit,
                command.BranchId,
                CreatedAt = utc,
                command.CreatedBy
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertSaleItem = """
                INSERT INTO SaleItems (Id, SaleId, ProductId, BatchId, Quantity, Price)
                VALUES (@Id, @SaleId, @ProductId, @BatchId, @Quantity, @Price)
                """;
            const string insertMovItem = """
                INSERT INTO StockMovementItems (Id, MovementId, ProductId, BatchId, Quantity, CostPrice)
                VALUES (@Id, @MovementId, @ProductId, @BatchId, @Quantity, @CostPrice)
                """;
            const string updateBatch = """
                UPDATE ProductBatches
                SET Quantity = @Quantity, UpdatedAt = @UpdatedAt, Active = @Active
                WHERE Id = @Id
                """;

            foreach (var line in command.Lines)
            {
                var batch = batches.First(b => b.Id == line.BatchId);
                await _session.Connection.ExecuteAsync(new CommandDefinition(insertSaleItem, new
                {
                    Id = Guid.NewGuid(),
                    SaleId = saleId,
                    line.ProductId,
                    line.BatchId,
                    line.Quantity,
                    Price = line.UnitSalePrice
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                batch.Quantity -= line.Quantity;
                batch.UpdatedAt = utc;
                if (batch.Quantity <= 0)
                {
                    batch.Quantity = 0;
                    batch.Active = false;
                }

                await _session.Connection.ExecuteAsync(new CommandDefinition(updateBatch, new
                {
                    batch.Id,
                    batch.Quantity,
                    batch.UpdatedAt,
                    batch.Active
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovItem, new
                {
                    Id = Guid.NewGuid(),
                    MovementId = movementId,
                    line.ProductId,
                    line.BatchId,
                    line.Quantity,
                    CostPrice = line.BatchCostPrice
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var sale = new Sale
        {
            Id = saleId,
            BranchId = command.BranchId,
            Total = total,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        return new RegisterSaleResult(sale, movementId);
    }

    public async Task<RegisterTransferResult> RegisterTransferAsync(
        RegisterTransferCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Quantity <= 0)
            throw new InvalidOperationException("Quantidade transferida deve ser maior que zero.");

        const string loadSource = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE Id = @Id
            """;
        var source = await _session.Connection
            .QuerySingleOrDefaultAsync<ProductBatch>(new CommandDefinition(loadSource, new { Id = command.SourceBatchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (source is null)
            throw new InvalidOperationException("Lote de origem não encontrado.");

        if (!source.Active || source.Quantity < command.Quantity)
            throw new InvalidOperationException("Quantidade indisponível no lote de origem.");

        if (source.BranchId == command.BranchDestId)
            throw new InvalidOperationException("Filial de origem e destino devem ser diferentes.");

        var utc = DateTime.UtcNow;
        var destBatchId = Guid.NewGuid();
        var movementId = Guid.NewGuid();

        source.Quantity -= command.Quantity;
        source.UpdatedAt = utc;
        if (source.Quantity <= 0)
        {
            source.Quantity = 0;
            source.Active = false;
        }

        await using var tx = await _session.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            const string updateSource = """
                UPDATE ProductBatches
                SET Quantity = @Quantity, UpdatedAt = @UpdatedAt, Active = @Active
                WHERE Id = @Id
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(updateSource, new
            {
                source.Id,
                source.Quantity,
                source.UpdatedAt,
                source.Active
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertDest = """
                INSERT INTO ProductBatches (Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active)
                VALUES (@Id, @ProductId, @BranchId, @Quantity, @InitialQuantity, @CostPrice, @ExpirationDate, @EntryDate, @CreatedAt, @UpdatedAt, @Active)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertDest, new
            {
                Id = destBatchId,
                source.ProductId,
                BranchId = command.BranchDestId,
                Quantity = command.Quantity,
                InitialQuantity = command.Quantity,
                source.CostPrice,
                source.ExpirationDate,
                EntryDate = utc,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertMovement = """
                INSERT INTO StockMovements (Id, Type, BranchId, BranchDestId, CreatedAt, CreatedBy)
                VALUES (@Id, @Type, @BranchId, @BranchDestId, @CreatedAt, @CreatedBy)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovement, new
            {
                Id = movementId,
                Type = (int)StockMovementType.Transfer,
                BranchId = source.BranchId,
                BranchDestId = command.BranchDestId,
                CreatedAt = utc,
                command.CreatedBy
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertMovItem = """
                INSERT INTO StockMovementItems (Id, MovementId, ProductId, BatchId, Quantity, CostPrice)
                VALUES (@Id, @MovementId, @ProductId, @BatchId, @Quantity, @CostPrice)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovItem, new
            {
                Id = Guid.NewGuid(),
                MovementId = movementId,
                ProductId = source.ProductId,
                BatchId = source.Id,
                Quantity = command.Quantity,
                CostPrice = source.CostPrice
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovItem, new
            {
                Id = Guid.NewGuid(),
                MovementId = movementId,
                ProductId = source.ProductId,
                BatchId = destBatchId,
                Quantity = command.Quantity,
                CostPrice = source.CostPrice
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var destBatch = new ProductBatch
        {
            Id = destBatchId,
            ProductId = source.ProductId,
            BranchId = command.BranchDestId,
            Quantity = command.Quantity,
            InitialQuantity = command.Quantity,
            CostPrice = source.CostPrice,
            ExpirationDate = source.ExpirationDate,
            EntryDate = utc,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        return new RegisterTransferResult(destBatch, movementId);
    }

    public async Task<RegisterProductionResult> RegisterProductionAsync(
        RegisterProductionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
            throw new InvalidOperationException("A produção deve ter ao menos uma linha.");

        var inputBatchIds = command.Lines.Select(l => l.BatchInputId).Distinct().ToArray();
        const string loadBatches = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE Id IN @Ids
            """;
        var trackedBatches = (await _session.Connection
                .QueryAsync<ProductBatch>(new CommandDefinition(loadBatches, new { Ids = inputBatchIds }, cancellationToken: cancellationToken))
                .ConfigureAwait(false))
            .ToList();

        foreach (var line in command.Lines)
        {
            var batch = trackedBatches.FirstOrDefault(b => b.Id == line.BatchInputId);
            if (batch is null)
                throw new InvalidOperationException($"Lote de entrada {line.BatchInputId} não encontrado.");

            if (batch.BranchId != command.BranchId)
                throw new InvalidOperationException("O lote de entrada não pertence à filial da produção.");

            if (batch.ProductId != line.ProductInputId)
                throw new InvalidOperationException("Produto de entrada não confere com o lote informado.");

            if (!batch.Active || batch.Quantity < line.QuantityInput)
                throw new InvalidOperationException($"Quantidade insuficiente no lote {line.BatchInputId}.");
        }

        var utc = DateTime.UtcNow;
        var productionId = Guid.NewGuid();
        var movementId = Guid.NewGuid();

        await using var tx = await _session.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            const string insertProd = """
                INSERT INTO Productions (Id, BranchId, CreatedAt, UpdatedAt, Active)
                VALUES (@Id, @BranchId, @CreatedAt, @UpdatedAt, @Active)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertProd, new
            {
                Id = productionId,
                command.BranchId,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string insertMovement = """
                INSERT INTO StockMovements (Id, Type, BranchId, BranchDestId, CreatedAt, CreatedBy)
                VALUES (@Id, @Type, @BranchId, NULL, @CreatedAt, @CreatedBy)
                """;
            await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovement, new
            {
                Id = movementId,
                Type = (int)StockMovementType.Production,
                command.BranchId,
                CreatedAt = utc,
                command.CreatedBy
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            const string updateBatch = """
                UPDATE ProductBatches
                SET Quantity = @Quantity, UpdatedAt = @UpdatedAt, Active = @Active
                WHERE Id = @Id
                """;
            const string insertBatch = """
                INSERT INTO ProductBatches (Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active)
                VALUES (@Id, @ProductId, @BranchId, @Quantity, @InitialQuantity, @CostPrice, @ExpirationDate, @EntryDate, @CreatedAt, @UpdatedAt, @Active)
                """;
            const string insertProdItem = """
                INSERT INTO ProductionItems (Id, ProductionId, ProductInputId, BatchInputId, QuantityInput, ProductOutputId, QuantityOutput, OutputBatchId)
                VALUES (@Id, @ProductionId, @ProductInputId, @BatchInputId, @QuantityInput, @ProductOutputId, @QuantityOutput, @OutputBatchId)
                """;
            const string insertMovItem = """
                INSERT INTO StockMovementItems (Id, MovementId, ProductId, BatchId, Quantity, CostPrice)
                VALUES (@Id, @MovementId, @ProductId, @BatchId, @Quantity, @CostPrice)
                """;

            foreach (var line in command.Lines)
            {
                var inputBatch = trackedBatches.First(b => b.Id == line.BatchInputId);
                var inputCost = inputBatch.CostPrice;

                inputBatch.Quantity -= line.QuantityInput;
                inputBatch.UpdatedAt = utc;
                if (inputBatch.Quantity <= 0)
                {
                    inputBatch.Quantity = 0;
                    inputBatch.Active = false;
                }

                await _session.Connection.ExecuteAsync(new CommandDefinition(updateBatch, new
                {
                    inputBatch.Id,
                    inputBatch.Quantity,
                    inputBatch.UpdatedAt,
                    inputBatch.Active
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                var outputBatchId = Guid.NewGuid();
                await _session.Connection.ExecuteAsync(new CommandDefinition(insertBatch, new
                {
                    Id = outputBatchId,
                    ProductId = line.ProductOutputId,
                    command.BranchId,
                    Quantity = line.QuantityOutput,
                    InitialQuantity = line.QuantityOutput,
                    CostPrice = line.OutputCostPricePerUnit,
                    ExpirationDate = line.OutputExpirationDate,
                    EntryDate = utc,
                    CreatedAt = utc,
                    UpdatedAt = utc,
                    Active = true
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                await _session.Connection.ExecuteAsync(new CommandDefinition(insertProdItem, new
                {
                    Id = Guid.NewGuid(),
                    ProductionId = productionId,
                    ProductInputId = line.ProductInputId,
                    BatchInputId = line.BatchInputId,
                    QuantityInput = line.QuantityInput,
                    ProductOutputId = line.ProductOutputId,
                    QuantityOutput = line.QuantityOutput,
                    OutputBatchId = outputBatchId
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovItem, new
                {
                    Id = Guid.NewGuid(),
                    MovementId = movementId,
                    ProductId = line.ProductInputId,
                    BatchId = line.BatchInputId,
                    Quantity = line.QuantityInput,
                    CostPrice = inputCost
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                await _session.Connection.ExecuteAsync(new CommandDefinition(insertMovItem, new
                {
                    Id = Guid.NewGuid(),
                    MovementId = movementId,
                    ProductId = line.ProductOutputId,
                    BatchId = outputBatchId,
                    Quantity = line.QuantityOutput,
                    CostPrice = line.OutputCostPricePerUnit
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var production = new Production
        {
            Id = productionId,
            BranchId = command.BranchId,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        return new RegisterProductionResult(production, movementId);
    }

    public async Task<Sale?> GetSaleByIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, BranchId, Total, CreatedAt, UpdatedAt, Active
            FROM Sales
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Sale>(new CommandDefinition(sql, new { Id = saleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SaleItem>> GetSaleItemsAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, SaleId, ProductId, BatchId, Quantity, Price
            FROM SaleItems
            WHERE SaleId = @SaleId
            """;
        var list = await _session.Connection
            .QueryAsync<SaleItem>(new CommandDefinition(sql, new { SaleId = saleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<IReadOnlyList<StockMovement>> ListMovementsAsync(
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Type, BranchId, BranchDestId, CreatedAt, CreatedBy
            FROM StockMovements
            WHERE @BranchId IS NULL OR BranchId = @BranchId OR BranchDestId = @BranchId
            ORDER BY CreatedAt DESC
            """;
        var list = await _session.Connection
            .QueryAsync<StockMovement>(new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<StockMovement?> GetMovementByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Type, BranchId, BranchDestId, CreatedAt, CreatedBy
            FROM StockMovements
            WHERE Id = @Id
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<StockMovement>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StockMovementItem>> GetMovementItemsAsync(
        Guid movementId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, MovementId, ProductId, BatchId, Quantity, CostPrice
            FROM StockMovementItems
            WHERE MovementId = @MovementId
            """;
        var list = await _session.Connection
            .QueryAsync<StockMovementItem>(new CommandDefinition(sql, new { MovementId = movementId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<IReadOnlyList<Sale>> ListSalesAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, BranchId, Total, CreatedAt, UpdatedAt, Active
            FROM Sales
            WHERE Active = 1 AND (@BranchId IS NULL OR BranchId = @BranchId)
            ORDER BY CreatedAt DESC
            """;
        var list = await _session.Connection
            .QueryAsync<Sale>(new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<bool> SoftDeleteSaleAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        const string selectSql = "SELECT Active FROM Sales WHERE Id = @Id";
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(selectSql, new { Id = saleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string updateSql = """
            UPDATE Sales SET Active = 0, UpdatedAt = @UpdatedAt WHERE Id = @Id AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(updateSql, new { Id = saleId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<Production>> ListProductionsAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, BranchId, CreatedAt, UpdatedAt, Active
            FROM Productions
            WHERE Active = 1 AND (@BranchId IS NULL OR BranchId = @BranchId)
            ORDER BY CreatedAt DESC
            """;
        var list = await _session.Connection
            .QueryAsync<Production>(new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Production?> GetProductionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, BranchId, CreatedAt, UpdatedAt, Active
            FROM Productions
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Production>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionItem>> GetProductionItemsAsync(
        Guid productionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductionId, ProductInputId, BatchInputId, QuantityInput, ProductOutputId, QuantityOutput, OutputBatchId
            FROM ProductionItems
            WHERE ProductionId = @ProductionId
            """;
        var list = await _session.Connection
            .QueryAsync<ProductionItem>(new CommandDefinition(sql, new { ProductionId = productionId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<bool> SoftDeleteProductionAsync(Guid productionId, CancellationToken cancellationToken = default)
    {
        const string selectSql = "SELECT Active FROM Productions WHERE Id = @Id";
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(selectSql, new { Id = productionId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string updateSql = """
            UPDATE Productions SET Active = 0, UpdatedAt = @UpdatedAt WHERE Id = @Id AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(updateSql, new { Id = productionId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }
}
