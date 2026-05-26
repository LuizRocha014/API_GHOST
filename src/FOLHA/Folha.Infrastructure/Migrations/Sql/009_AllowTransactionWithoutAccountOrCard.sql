-- ============================================================================
-- 009_AllowTransactionWithoutAccountOrCard
-- O app registra gastos avulsos (Pix/dinheiro) sem vincular conta nem cartão.
-- A constraint CK_FOLHA_Tx_target exigia account_id OU credit_card_id, fazendo
-- esses gastos falharem no INSERT ("Informe AccountId ou CreditCardId").
-- Removemos o CHECK para permitir transação sem origem vinculada.
-- ============================================================================

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_FOLHA_Tx_target'
      AND parent_object_id = OBJECT_ID(N'dbo.FOLHA_Transactions')
)
BEGIN
    ALTER TABLE dbo.FOLHA_Transactions DROP CONSTRAINT CK_FOLHA_Tx_target;
END
GO
