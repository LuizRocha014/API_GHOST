-- ============================================================================
-- 007_AddCreditCardInstallments
-- Planos de parcelamento fixos por cartão (ex.: "Geladeira em 10x de R$200,
-- já paguei 3"). A cada virada do cartão o app materializa a parcela do mês
-- como uma transação normal — esta tabela guarda só o "molde" do parcelamento.
--
-- - installment_total   : total de parcelas (N).
-- - installments_paid   : parcelas já pagas no momento do cadastro (P). As
--                         parcelas geradas automaticamente começam em P+1.
-- - installment_amount  : valor de CADA parcela.
-- - start_date          : data da 1ª parcela gerada automaticamente (parcela
--                         P+1). O app calcula quantas já venceram a partir daqui.
--
-- Obs.: o FK para FOLHA_Users é SEM cascade de propósito — a remoção em cascata
-- já chega via FOLHA_CreditCards (FK_FOLHA_CCI_card). Dois caminhos de cascade
-- até FOLHA_Users seriam rejeitados pelo SQL Server.
-- ============================================================================

IF OBJECT_ID(N'dbo.FOLHA_CreditCardInstallments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_CreditCardInstallments
    (
        id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_CCI_id DEFAULT NEWSEQUENTIALID(),
        user_id            UNIQUEIDENTIFIER NOT NULL,
        credit_card_id     UNIQUEIDENTIFIER NOT NULL,
        description        NVARCHAR(150)    NOT NULL,
        installment_total  SMALLINT         NOT NULL,
        installments_paid  SMALLINT         NOT NULL CONSTRAINT DF_FOLHA_CCI_paid DEFAULT (0),
        installment_amount DECIMAL(18,2)    NOT NULL,
        start_date         DATE             NOT NULL,
        created_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_CCI_created DEFAULT (SYSUTCDATETIME()),
        updated_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_CCI_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_CreditCardInstallments PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_CCI_user FOREIGN KEY (user_id)        REFERENCES dbo.FOLHA_Users(id),
        CONSTRAINT FK_FOLHA_CCI_card FOREIGN KEY (credit_card_id) REFERENCES dbo.FOLHA_CreditCards(id) ON DELETE CASCADE,
        CONSTRAINT CK_FOLHA_CCI_total  CHECK (installment_total >= 1),
        CONSTRAINT CK_FOLHA_CCI_paid   CHECK (installments_paid >= 0 AND installments_paid <= installment_total),
        CONSTRAINT CK_FOLHA_CCI_amount CHECK (installment_amount > 0)
    );

    CREATE INDEX IX_FOLHA_CCI_card ON dbo.FOLHA_CreditCardInstallments(credit_card_id);
    CREATE INDEX IX_FOLHA_CCI_user ON dbo.FOLHA_CreditCardInstallments(user_id);
END
GO

PRINT 'FOLHA_CreditCardInstallments criada/atualizada com sucesso.';
GO
