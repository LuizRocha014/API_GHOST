using Application.Abstractions;
using Domain;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly object _sync = new();
    private readonly List<ProductBatch> _batches = new();
    private readonly List<StockMovement> _movements = new();
    private readonly List<StockMovementItem> _movementItems = new();
    private readonly List<Sale> _sales = new();
    private readonly List<SaleItem> _saleItems = new();
    private readonly List<Production> _productions = new();
    private readonly List<ProductionItem> _productionItems = new();

    public Task<decimal> GetTotalStockForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var sum = _batches
                .Where(b => b.ProductId == productId && b.Active)
                .Sum(b => b.Quantity);
            return Task.FromResult(sum);
        }
    }

    public Task<IReadOnlyList<ProductBatch>> GetBatchesForProductAndBranchAsync(
        Guid productId,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<ProductBatch> list = _batches
                .Where(b => b.ProductId == productId && b.BranchId == branchId && b.Active && b.Quantity > 0)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<IReadOnlyList<ProductBatch>> ListAllBatchesForProductAndBranchAsync(
        Guid productId,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<ProductBatch> list = _batches
                .Where(b => b.ProductId == productId && b.BranchId == branchId)
                .OrderBy(b => b.EntryDate)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var b = _batches.FirstOrDefault(x => x.Id == batchId);
            return Task.FromResult(b);
        }
    }

    public Task<RegisterEntryResult> RegisterEntryAsync(RegisterEntryCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Quantity <= 0)
            throw new InvalidOperationException("Quantidade de entrada deve ser maior que zero.");

        var utc = DateTime.UtcNow;
        lock (_sync)
        {
            var batch = new ProductBatch
            {
                Id = Guid.NewGuid(),
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
            _batches.Add(batch);

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                Type = StockMovementType.Entry,
                BranchId = command.BranchId,
                BranchDestId = null,
                CreatedAt = utc,
                CreatedBy = command.CreatedBy
            };
            _movements.Add(movement);

            _movementItems.Add(new StockMovementItem
            {
                Id = Guid.NewGuid(),
                MovementId = movement.Id,
                ProductId = command.ProductId,
                BatchId = batch.Id,
                Quantity = command.Quantity,
                CostPrice = command.CostPrice
            });

            return Task.FromResult(new RegisterEntryResult(batch, movement.Id));
        }
    }

    public Task<RegisterSaleResult> RegisterSaleAsync(RegisterSaleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
            throw new InvalidOperationException("A venda deve ter ao menos um item.");

        var utc = DateTime.UtcNow;
        lock (_sync)
        {
            foreach (var line in command.Lines)
            {
                var batch = _batches.FirstOrDefault(b => b.Id == line.BatchId);
                if (batch is null)
                    throw new InvalidOperationException($"Lote {line.BatchId} não encontrado.");

                if (batch.BranchId != command.BranchId)
                    throw new InvalidOperationException($"O lote {line.BatchId} não pertence à filial da venda.");

                if (batch.Quantity < line.Quantity)
                    throw new InvalidOperationException(
                        $"Estoque insuficiente no lote {line.BatchId}. Disponível: {batch.Quantity}, solicitado: {line.Quantity}.");
            }

            var total = command.Lines.Sum(l => l.Quantity * l.UnitSalePrice);

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                BranchId = command.BranchId,
                Total = total,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            };
            _sales.Add(sale);

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                Type = StockMovementType.Exit,
                BranchId = command.BranchId,
                BranchDestId = null,
                CreatedAt = utc,
                CreatedBy = command.CreatedBy
            };
            _movements.Add(movement);

            foreach (var line in command.Lines)
            {
                _saleItems.Add(new SaleItem
                {
                    Id = Guid.NewGuid(),
                    SaleId = sale.Id,
                    ProductId = line.ProductId,
                    BatchId = line.BatchId,
                    Quantity = line.Quantity,
                    Price = line.UnitSalePrice
                });

                var batch = _batches.First(b => b.Id == line.BatchId);
                batch.Quantity -= line.Quantity;
                batch.UpdatedAt = utc;
                if (batch.Quantity <= 0)
                {
                    batch.Quantity = 0;
                    batch.Active = false;
                }

                _movementItems.Add(new StockMovementItem
                {
                    Id = Guid.NewGuid(),
                    MovementId = movement.Id,
                    ProductId = line.ProductId,
                    BatchId = line.BatchId,
                    Quantity = line.Quantity,
                    CostPrice = line.BatchCostPrice
                });
            }

            return Task.FromResult(new RegisterSaleResult(sale, movement.Id));
        }
    }

    public Task<RegisterTransferResult> RegisterTransferAsync(RegisterTransferCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Quantity <= 0)
            throw new InvalidOperationException("Quantidade transferida deve ser maior que zero.");

        var utc = DateTime.UtcNow;
        lock (_sync)
        {
            var source = _batches.FirstOrDefault(b => b.Id == command.SourceBatchId);
            if (source is null)
                throw new InvalidOperationException("Lote de origem não encontrado.");

            if (!source.Active || source.Quantity < command.Quantity)
                throw new InvalidOperationException("Quantidade indisponível no lote de origem.");

            if (source.BranchId == command.BranchDestId)
                throw new InvalidOperationException("Filial de origem e destino devem ser diferentes.");

            source.Quantity -= command.Quantity;
            source.UpdatedAt = utc;
            if (source.Quantity <= 0)
            {
                source.Quantity = 0;
                source.Active = false;
            }

            var destBatch = new ProductBatch
            {
                Id = Guid.NewGuid(),
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
            _batches.Add(destBatch);

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                Type = StockMovementType.Transfer,
                BranchId = source.BranchId,
                BranchDestId = command.BranchDestId,
                CreatedAt = utc,
                CreatedBy = command.CreatedBy
            };
            _movements.Add(movement);

            _movementItems.Add(new StockMovementItem
            {
                Id = Guid.NewGuid(),
                MovementId = movement.Id,
                ProductId = source.ProductId,
                BatchId = source.Id,
                Quantity = command.Quantity,
                CostPrice = source.CostPrice
            });

            _movementItems.Add(new StockMovementItem
            {
                Id = Guid.NewGuid(),
                MovementId = movement.Id,
                ProductId = source.ProductId,
                BatchId = destBatch.Id,
                Quantity = command.Quantity,
                CostPrice = source.CostPrice
            });

            return Task.FromResult(new RegisterTransferResult(destBatch, movement.Id));
        }
    }

    public Task<RegisterProductionResult> RegisterProductionAsync(RegisterProductionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
            throw new InvalidOperationException("A produção deve ter ao menos uma linha.");

        var utc = DateTime.UtcNow;
        lock (_sync)
        {
            foreach (var line in command.Lines)
            {
                var batch = _batches.FirstOrDefault(b => b.Id == line.BatchInputId);
                if (batch is null)
                    throw new InvalidOperationException($"Lote de entrada {line.BatchInputId} não encontrado.");

                if (batch.BranchId != command.BranchId)
                    throw new InvalidOperationException("O lote de entrada não pertence à filial da produção.");

                if (batch.ProductId != line.ProductInputId)
                    throw new InvalidOperationException("Produto de entrada não confere com o lote informado.");

                if (!batch.Active || batch.Quantity < line.QuantityInput)
                    throw new InvalidOperationException($"Quantidade insuficiente no lote {line.BatchInputId}.");
            }

            var production = new Production
            {
                Id = Guid.NewGuid(),
                BranchId = command.BranchId,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            };
            _productions.Add(production);

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                Type = StockMovementType.Production,
                BranchId = command.BranchId,
                BranchDestId = null,
                CreatedAt = utc,
                CreatedBy = command.CreatedBy
            };
            _movements.Add(movement);

            foreach (var line in command.Lines)
            {
                var inputBatch = _batches.First(b => b.Id == line.BatchInputId);
                inputBatch.Quantity -= line.QuantityInput;
                inputBatch.UpdatedAt = utc;
                if (inputBatch.Quantity <= 0)
                {
                    inputBatch.Quantity = 0;
                    inputBatch.Active = false;
                }

                var outputBatch = new ProductBatch
                {
                    Id = Guid.NewGuid(),
                    ProductId = line.ProductOutputId,
                    BranchId = command.BranchId,
                    Quantity = line.QuantityOutput,
                    InitialQuantity = line.QuantityOutput,
                    CostPrice = line.OutputCostPricePerUnit,
                    ExpirationDate = line.OutputExpirationDate,
                    EntryDate = utc,
                    CreatedAt = utc,
                    UpdatedAt = utc,
                    Active = true
                };
                _batches.Add(outputBatch);

                _productionItems.Add(new ProductionItem
                {
                    Id = Guid.NewGuid(),
                    ProductionId = production.Id,
                    ProductInputId = line.ProductInputId,
                    BatchInputId = line.BatchInputId,
                    QuantityInput = line.QuantityInput,
                    ProductOutputId = line.ProductOutputId,
                    QuantityOutput = line.QuantityOutput,
                    OutputBatchId = outputBatch.Id
                });

                _movementItems.Add(new StockMovementItem
                {
                    Id = Guid.NewGuid(),
                    MovementId = movement.Id,
                    ProductId = line.ProductInputId,
                    BatchId = line.BatchInputId,
                    Quantity = line.QuantityInput,
                    CostPrice = inputBatch.CostPrice
                });

                _movementItems.Add(new StockMovementItem
                {
                    Id = Guid.NewGuid(),
                    MovementId = movement.Id,
                    ProductId = line.ProductOutputId,
                    BatchId = outputBatch.Id,
                    Quantity = line.QuantityOutput,
                    CostPrice = line.OutputCostPricePerUnit
                });
            }

            return Task.FromResult(new RegisterProductionResult(production, movement.Id));
        }
    }

    public Task<Sale?> GetSaleByIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var s = _sales.FirstOrDefault(x => x.Id == saleId && x.Active);
            return Task.FromResult(s);
        }
    }

    public Task<IReadOnlyList<SaleItem>> GetSaleItemsAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<SaleItem> list = _saleItems.Where(i => i.SaleId == saleId).ToList();
            return Task.FromResult(list);
        }
    }

    public Task<IReadOnlyList<StockMovement>> ListMovementsAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IEnumerable<StockMovement> q = _movements.OrderByDescending(m => m.CreatedAt);
            if (branchId.HasValue)
                q = q.Where(m => m.BranchId == branchId || m.BranchDestId == branchId);
            return Task.FromResult<IReadOnlyList<StockMovement>>(q.ToList());
        }
    }

    public Task<StockMovement?> GetMovementByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var m = _movements.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(m);
        }
    }

    public Task<IReadOnlyList<StockMovementItem>> GetMovementItemsAsync(
        Guid movementId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<StockMovementItem> list = _movementItems.Where(i => i.MovementId == movementId).ToList();
            return Task.FromResult(list);
        }
    }

    public Task<IReadOnlyList<Sale>> ListSalesAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IEnumerable<Sale> q = _sales.Where(s => s.Active).OrderByDescending(s => s.CreatedAt);
            if (branchId.HasValue)
                q = q.Where(s => s.BranchId == branchId.Value);
            return Task.FromResult<IReadOnlyList<Sale>>(q.ToList());
        }
    }

    public Task<bool> SoftDeleteSaleAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var s = _sales.FirstOrDefault(x => x.Id == saleId);
            if (s is null || !s.Active)
                return Task.FromResult(s is not null);

            s.Active = false;
            s.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<Production>> ListProductionsAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IEnumerable<Production> q = _productions.Where(p => p.Active).OrderByDescending(p => p.CreatedAt);
            if (branchId.HasValue)
                q = q.Where(p => p.BranchId == branchId.Value);
            return Task.FromResult<IReadOnlyList<Production>>(q.ToList());
        }
    }

    public Task<Production?> GetProductionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var p = _productions.FirstOrDefault(x => x.Id == id && x.Active);
            return Task.FromResult(p);
        }
    }

    public Task<IReadOnlyList<ProductionItem>> GetProductionItemsAsync(
        Guid productionId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<ProductionItem> list = _productionItems.Where(i => i.ProductionId == productionId).ToList();
            return Task.FromResult(list);
        }
    }

    public Task<bool> SoftDeleteProductionAsync(Guid productionId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var p = _productions.FirstOrDefault(x => x.Id == productionId);
            if (p is null || !p.Active)
                return Task.FromResult(p is not null);

            p.Active = false;
            p.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }
}
