-- ============================================================================
-- 006_AllowCreditCardWithoutAccount
-- Torna `account_id` opcional em FOLHA_CreditCards. Permite cadastrar um cartão
-- sem vincular a uma conta. Como há FK para FOLHA_Accounts, precisamos remover
-- a constraint, alterar a coluna para NULL e recriar a FK (FK aceita NULL).
-- ============================================================================

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_FOLHA_CreditCards_account'
      AND parent_object_id = Object_ID(N'dbo.FOLHA_CreditCards')
)
BEGIN
    ALTER TABLE dbo.FOLHA_CreditCards DROP CONSTRAINT FK_FOLHA_CreditCards_account;
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'account_id'
      AND Object_ID = Object_ID(N'dbo.FOLHA_CreditCards')
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.FOLHA_CreditCards ALTER COLUMN account_id UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_FOLHA_CreditCards_account'
      AND parent_object_id = Object_ID(N'dbo.FOLHA_CreditCards')
)
BEGIN
    ALTER TABLE dbo.FOLHA_CreditCards
        ADD CONSTRAINT FK_FOLHA_CreditCards_account
        FOREIGN KEY (account_id) REFERENCES dbo.FOLHA_Accounts(id);
END
GO
