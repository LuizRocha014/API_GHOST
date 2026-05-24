-- ============================================================================
-- 004_AddBillPartialAndInstallments
-- Adiciona suporte a pagamento parcial e a parcelamento informativo
-- (parcela X de Y) na tabela FOLHA_Bills.
--
-- - paid_amount         : valor acumulado já quitado da conta. Quando atinge
--                         `amount`, a conta é promovida para paid/received.
-- - installment_current : número da parcela atual (1..N) — opcional.
-- - installment_total   : total de parcelas — opcional.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'paid_amount'
      AND Object_ID = Object_ID(N'dbo.FOLHA_Bills')
)
BEGIN
    ALTER TABLE dbo.FOLHA_Bills
        ADD paid_amount DECIMAL(19,4) NOT NULL CONSTRAINT DF_FOLHA_Bills_paid_amount DEFAULT (0);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'installment_current'
      AND Object_ID = Object_ID(N'dbo.FOLHA_Bills')
)
BEGIN
    ALTER TABLE dbo.FOLHA_Bills
        ADD installment_current SMALLINT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'installment_total'
      AND Object_ID = Object_ID(N'dbo.FOLHA_Bills')
)
BEGIN
    ALTER TABLE dbo.FOLHA_Bills
        ADD installment_total SMALLINT NULL;
END
GO
