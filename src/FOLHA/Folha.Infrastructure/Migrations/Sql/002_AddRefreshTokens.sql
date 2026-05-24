/*
    Folha — migration 002: tabela de refresh tokens
    Cada login emite um refresh token de longa duração (ex.: 30 dias).
    O token armazenado é apenas o hash (SHA-256) — nunca o token em claro.
*/

IF OBJECT_ID(N'dbo.FOLHA_RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_RefreshTokens
    (
        id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_RT_id DEFAULT NEWSEQUENTIALID(),
        user_id               UNIQUEIDENTIFIER NOT NULL,
        token_hash            NVARCHAR(255)    NOT NULL,
        device_id             NVARCHAR(128)    NULL,
        user_agent            NVARCHAR(500)    NULL,
        ip_address            NVARCHAR(45)     NULL,
        expires_at            DATETIME2(3)     NOT NULL,
        revoked_at            DATETIME2(3)     NULL,
        replaced_by_token_id  UNIQUEIDENTIFIER NULL,
        created_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_RT_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_RefreshTokens PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_RT_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT UQ_FOLHA_RT_hash UNIQUE (token_hash)
    );

    CREATE INDEX IX_FOLHA_RT_user_active ON dbo.FOLHA_RefreshTokens(user_id) WHERE revoked_at IS NULL;
END
GO
