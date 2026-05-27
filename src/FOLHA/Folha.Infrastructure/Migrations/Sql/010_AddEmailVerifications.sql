/*
    Folha — migration 010: códigos de verificação de e-mail (OTP de cadastro).
    Guardamos apenas o hash do código. Um usuário pode ter vários registros ao
    longo do tempo (reenvios); o ativo é o mais recente com consumed_at NULL.
    Script idempotente.
*/

------------------------------------------------------------
-- FOLHA_EmailVerifications
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_EmailVerifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_EmailVerifications
    (
        id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_EmailVerif_id DEFAULT NEWSEQUENTIALID(),
        user_id      UNIQUEIDENTIFIER NOT NULL,
        code_hash    NVARCHAR(255)    NOT NULL,
        expires_at   DATETIME2(3)     NOT NULL,
        consumed_at  DATETIME2(3)     NULL,
        attempts     INT              NOT NULL CONSTRAINT DF_FOLHA_EmailVerif_attempts DEFAULT (0),
        created_at   DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_EmailVerif_created  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_EmailVerifications PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_EmailVerif_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_FOLHA_EmailVerif_user_active
        ON dbo.FOLHA_EmailVerifications(user_id, consumed_at, created_at DESC);
END
GO

PRINT 'Migration 010 (FOLHA_EmailVerifications) aplicada.';
GO
