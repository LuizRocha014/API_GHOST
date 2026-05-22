using Application.Abstractions;
using Domain;
using Domain.Entities;

namespace Application.Inventory;

public sealed class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventory;
    private readonly IProductRepository _products;
    private readonly IBranchRepository _branches;

    public InventoryService(
        IInventoryRepository inventory,
        IProductRepository products,
        IBranchRepository branches)
    {
        _inventory = inventory;
        _products = products;
        _branches = branches;
    }

    public async Task<StockEntryResponse> RegisterEntryAsync(
        StockEntryRequest request,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchAsync(request.BranchId, cancellationToken).ConfigureAwait(false);

        if (request.Quantity <= 0)
            throw new InvalidOperationException("Quantidade de entrada deve ser maior que zero.");

        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false)
                      ?? throw new InvalidOperationException("Produto não encontrado.");

        if (product.IsPerishable && !request.ExpirationDate.HasValue)
            throw new InvalidOperationException("Produto perecível exige data de validade.");

        if (!UnitTypes.IsValid(product.UnitType))
            throw new InvalidOperationException($"Unidade do produto inválida: {product.UnitType}.");

        var entryDate = request.EntryDate ?? DateTime.UtcNow;

        var cmd = new RegisterEntryCommand(
            request.ProductId,
            request.BranchId,
            request.Quantity,
            request.CostPrice,
            request.ExpirationDate,
            entryDate,
            createdBy);

        var result = await _inventory.RegisterEntryAsync(cmd, cancellationToken).ConfigureAwait(false);
        return new StockEntryResponse(result.Batch.Id, result.StockMovementId);
    }

    public async Task<CreateSaleResponse> RegisterSaleAsync(
        CreateSaleRequest request,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchAsync(request.BranchId, cancellationToken).ConfigureAwait(false);

        if (request.Lines.Count == 0)
            throw new InvalidOperationException("Informe ao menos um item na venda.");

        var registerLines = new List<RegisterSaleLine>();

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Quantidade do item deve ser maior que zero.");

            var product = await _products.GetByIdAsync(line.ProductId, cancellationToken).ConfigureAwait(false)
                          ?? throw new InvalidOperationException($"Produto {line.ProductId} não encontrado.");

            var unitPrice = line.UnitPrice ?? product.SalePrice;

            var batches = await _inventory
                .GetBatchesForProductAndBranchAsync(line.ProductId, request.BranchId, cancellationToken)
                .ConfigureAwait(false);

            var ordered = OrderBatchesFefo(batches, product);
            var remaining = line.Quantity;
            foreach (var batch in ordered)
            {
                if (remaining <= 0)
                    break;

                var take = Math.Min(batch.Quantity, remaining);
                registerLines.Add(new RegisterSaleLine(
                    line.ProductId,
                    batch.Id,
                    take,
                    unitPrice,
                    batch.CostPrice));
                remaining -= take;
            }

            if (remaining > 0)
                throw new InvalidOperationException(
                    $"Estoque insuficiente para o produto {product.Name} (SKU {product.Sku}). Faltam {remaining} unidades.");
        }

        var saleResult = await _inventory
            .RegisterSaleAsync(new RegisterSaleCommand(request.BranchId, createdBy, registerLines), cancellationToken)
            .ConfigureAwait(false);

        return new CreateSaleResponse(saleResult.Sale.Id, saleResult.StockMovementId);
    }

    public async Task<TransferStockResponse> RegisterTransferAsync(
        TransferStockRequest request,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        var source = await _inventory.GetBatchAsync(request.SourceBatchId, cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Lote de origem não encontrado.");

        await ValidateBranchAsync(source.BranchId, cancellationToken).ConfigureAwait(false);
        await ValidateBranchAsync(request.BranchDestId, cancellationToken).ConfigureAwait(false);

        var result = await _inventory
            .RegisterTransferAsync(
                new RegisterTransferCommand(request.SourceBatchId, request.BranchDestId, request.Quantity, createdBy),
                cancellationToken)
            .ConfigureAwait(false);

        return new TransferStockResponse(result.DestinationBatch.Id, result.StockMovementId);
    }

    public async Task<CreateProductionResponse> RegisterProductionAsync(
        CreateProductionRequest request,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchAsync(request.BranchId, cancellationToken).ConfigureAwait(false);

        if (request.Lines.Count == 0)
            throw new InvalidOperationException("Informe ao menos uma linha de produção.");

        var lines = new List<RegisterProductionLine>();

        foreach (var line in request.Lines)
        {
            if (line.QuantityInput <= 0 || line.QuantityOutput <= 0)
                throw new InvalidOperationException("Quantidades de entrada e saída devem ser maiores que zero.");

            var inputBatch = await _inventory.GetBatchAsync(line.BatchInputId, cancellationToken).ConfigureAwait(false)
                             ?? throw new InvalidOperationException("Lote de entrada não encontrado.");

            var inputProduct = await _products.GetByIdAsync(line.ProductInputId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException("Produto de entrada não encontrado.");

            var outputProduct = await _products.GetByIdAsync(line.ProductOutputId, cancellationToken).ConfigureAwait(false)
                                ?? throw new InvalidOperationException("Produto de saída não encontrado.");

            if (outputProduct.IsPerishable && !line.OutputExpirationDate.HasValue)
                throw new InvalidOperationException(
                    $"Produto de saída perecível ({outputProduct.Name}) exige data de validade do lote produzido.");

            var totalInputCost = line.QuantityInput * inputBatch.CostPrice;
            var outputUnitCost = totalInputCost / line.QuantityOutput;

            lines.Add(new RegisterProductionLine(
                line.ProductInputId,
                line.BatchInputId,
                line.QuantityInput,
                line.ProductOutputId,
                line.QuantityOutput,
                outputUnitCost,
                line.OutputExpirationDate));
        }

        var result = await _inventory
            .RegisterProductionAsync(new RegisterProductionCommand(request.BranchId, createdBy, lines), cancellationToken)
            .ConfigureAwait(false);

        return new CreateProductionResponse(result.Production.Id, result.StockMovementId);
    }

    public async Task<SaleDetailDto?> GetSaleDetailAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _inventory.GetSaleByIdAsync(saleId, cancellationToken).ConfigureAwait(false);
        if (sale is null)
            return null;

        var items = await _inventory.GetSaleItemsAsync(saleId, cancellationToken).ConfigureAwait(false);
        var details = new List<SaleLineDetailDto>();
        decimal profitTotal = 0;

        foreach (var i in items)
        {
            var batch = await _inventory.GetBatchAsync(i.BatchId, cancellationToken).ConfigureAwait(false);
            var cost = batch?.CostPrice ?? 0;
            var lineProfit = (i.Price - cost) * i.Quantity;
            profitTotal += lineProfit;

            details.Add(new SaleLineDetailDto(
                i.ProductId,
                i.BatchId,
                i.Quantity,
                i.Price,
                cost,
                lineProfit));
        }

        return new SaleDetailDto(sale.Id, sale.BranchId, sale.Total, profitTotal, details, sale.CreatedAt);
    }

    public async Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var list = await _inventory.ListMovementsAsync(branchId, cancellationToken).ConfigureAwait(false);
        return list
            .Select(m => new StockMovementDto(m.Id, m.Type, m.BranchId, m.BranchDestId, m.CreatedAt, m.CreatedBy))
            .ToList();
    }

    public async Task<IReadOnlyList<ProductBatchDto>> ListBatchesAsync(
        Guid productId,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var batches = await _inventory
            .ListAllBatchesForProductAndBranchAsync(productId, branchId, cancellationToken)
            .ConfigureAwait(false);

        return batches
            .Select(b => new ProductBatchDto(
                b.Id,
                b.ProductId,
                b.BranchId,
                b.Quantity,
                b.InitialQuantity,
                b.CostPrice,
                b.ExpirationDate,
                b.EntryDate,
                b.Active))
            .ToList();
    }

    public async Task<StockMovementDetailDto?> GetMovementDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _inventory.GetMovementByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (m is null)
            return null;

        var items = await _inventory.GetMovementItemsAsync(m.Id, cancellationToken).ConfigureAwait(false);
        var itemDtos = items
            .Select(i => new StockMovementItemDto(i.Id, i.ProductId, i.BatchId, i.Quantity, i.CostPrice))
            .ToList();

        return new StockMovementDetailDto(
            m.Id,
            m.Type,
            m.BranchId,
            m.BranchDestId,
            m.CreatedAt,
            m.CreatedBy,
            itemDtos);
    }

    public async Task<IReadOnlyList<SaleListDto>> ListSalesAsync(
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var sales = await _inventory.ListSalesAsync(branchId, cancellationToken).ConfigureAwait(false);
        return sales.Select(s => new SaleListDto(s.Id, s.BranchId, s.Total, s.CreatedAt)).ToList();
    }

    public Task<bool> SoftDeleteSaleAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        _inventory.SoftDeleteSaleAsync(saleId, cancellationToken);

    public async Task<IReadOnlyList<ProductionListDto>> ListProductionsAsync(
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var list = await _inventory.ListProductionsAsync(branchId, cancellationToken).ConfigureAwait(false);
        return list.Select(p => new ProductionListDto(p.Id, p.BranchId, p.CreatedAt)).ToList();
    }

    public async Task<ProductionDetailDto?> GetProductionDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _inventory.GetProductionByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (p is null)
            return null;

        var items = await _inventory.GetProductionItemsAsync(id, cancellationToken).ConfigureAwait(false);
        var lines = items
            .Select(i => new ProductionItemDetailDto(
                i.Id,
                i.ProductInputId,
                i.BatchInputId,
                i.QuantityInput,
                i.ProductOutputId,
                i.QuantityOutput,
                i.OutputBatchId))
            .ToList();

        return new ProductionDetailDto(p.Id, p.BranchId, p.CreatedAt, lines);
    }

    public Task<bool> SoftDeleteProductionAsync(Guid productionId, CancellationToken cancellationToken = default) =>
        _inventory.SoftDeleteProductionAsync(productionId, cancellationToken);

    private static IReadOnlyList<ProductBatch> OrderBatchesFefo(IReadOnlyList<ProductBatch> batches, Product product)
    {
        var today = DateTime.UtcNow.Date;

        IEnumerable<ProductBatch> filtered = batches;
        if (product.IsPerishable)
            filtered = batches.Where(b => b.ExpirationDate is null || b.ExpirationDate.Value.Date >= today);

        return filtered
            .OrderBy(b => b.ExpirationDate ?? DateTime.MaxValue)
            .ThenBy(b => b.EntryDate)
            .ToList();
    }

    private async Task ValidateBranchAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var b = await _branches.GetByIdAsync(branchId, cancellationToken).ConfigureAwait(false);
        if (b is null)
            throw new InvalidOperationException("Filial não encontrada.");
    }
}
