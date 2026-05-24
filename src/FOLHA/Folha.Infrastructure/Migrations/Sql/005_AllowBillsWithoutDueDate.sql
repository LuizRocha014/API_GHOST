-- ============================================================================
-- 005_AllowBillsWithoutDueDate
-- Torna `due_date` opcional em FOLHA_Bills. Permite cadastrar contas avulsas
-- ainda sem data definida (ex.: orçamento aberto, parcela ainda sem boleto).
--
-- O índice IX_FOLHA_Bills_user_due continua válido — SQL Server aceita NULL
-- na coluna indexada.
-- ============================================================================

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'due_date'
      AND Object_ID = Object_ID(N'dbo.FOLHA_Bills')
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.FOLHA_Bills ALTER COLUMN due_date DATE NULL;
END
GO
