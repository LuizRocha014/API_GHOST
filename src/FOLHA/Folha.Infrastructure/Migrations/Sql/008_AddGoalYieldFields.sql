-- ============================================================================
-- 008_AddGoalYieldFields
-- Campos de rendimento das metas, que até agora só existiam no app (local) e se
-- perdiam no pull. Agora trafegam para o servidor.
--
-- - monthly_yield_percent : rendimento mensal estimado (% a.m.), opcional.
-- - is_cdb                : marca a meta como aplicação tipo CDB.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'monthly_yield_percent'
      AND Object_ID = Object_ID(N'dbo.FOLHA_Goals')
)
BEGIN
    ALTER TABLE dbo.FOLHA_Goals
        ADD monthly_yield_percent DECIMAL(9,4) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'is_cdb'
      AND Object_ID = Object_ID(N'dbo.FOLHA_Goals')
)
BEGIN
    ALTER TABLE dbo.FOLHA_Goals
        ADD is_cdb BIT NOT NULL CONSTRAINT DF_FOLHA_Goals_is_cdb DEFAULT (0);
END
GO
