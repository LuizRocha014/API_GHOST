/*
    Abasta — migration 003: tabelas de "acessos".
      - ABASTA_Invitations : gestor convida colaboradores para a empresa (onboarding).
      - ABASTA_AccessLogs  : auditoria de login/logout/refresh (quem acessou, quando, de onde).
    Idempotente.
*/

------------------------------------------------------------
-- ABASTA_Invitations
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Invitations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Invitations
    (
        id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Inv_id DEFAULT NEWSEQUENTIALID(),
        company_id         UNIQUEIDENTIFIER NOT NULL,
        email              NVARCHAR(255)    NOT NULL,
        role               VARCHAR(20)      NOT NULL CONSTRAINT DF_ABASTA_Inv_role   DEFAULT ('collaborator'),
        invited_by_user_id UNIQUEIDENTIFIER NOT NULL,
        token_hash         NVARCHAR(255)    NOT NULL,
        status             VARCHAR(15)      NOT NULL CONSTRAINT DF_ABASTA_Inv_status DEFAULT ('pending'),
        expires_at         DATETIME2(3)     NOT NULL,
        accepted_at        DATETIME2(3)     NULL,
        accepted_user_id   UNIQUEIDENTIFIER NULL,
        created_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Inv_created DEFAULT (SYSUTCDATETIME()),
        updated_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Inv_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Invitations PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Inv_company  FOREIGN KEY (company_id)         REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_Inv_inviter  FOREIGN KEY (invited_by_user_id) REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT FK_ABASTA_Inv_accepted FOREIGN KEY (accepted_user_id)   REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT UQ_ABASTA_Inv_token UNIQUE (token_hash),
        CONSTRAINT CK_ABASTA_Inv_role   CHECK (role   IN ('collaborator','admin')),
        CONSTRAINT CK_ABASTA_Inv_status CHECK (status IN ('pending','accepted','revoked','expired'))
    );

    CREATE INDEX IX_ABASTA_Inv_company ON dbo.ABASTA_Invitations(company_id, status, created_at DESC);
    -- No máximo um convite pendente por e-mail/empresa.
    CREATE UNIQUE INDEX UX_ABASTA_Inv_pending ON dbo.ABASTA_Invitations(company_id, email) WHERE status = 'pending';
END
GO

------------------------------------------------------------
-- ABASTA_AccessLogs  (auditoria de acesso)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_AccessLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_AccessLogs
    (
        id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_AL_id DEFAULT NEWSEQUENTIALID(),
        company_id  UNIQUEIDENTIFIER NULL,
        user_id     UNIQUEIDENTIFIER NULL,
        email       NVARCHAR(255)    NULL,
        event       VARCHAR(30)      NOT NULL,
        succeeded   BIT              NOT NULL CONSTRAINT DF_ABASTA_AL_ok DEFAULT (1),
        ip_address  NVARCHAR(45)     NULL,
        user_agent  NVARCHAR(500)    NULL,
        detail      NVARCHAR(200)    NULL,
        created_at  DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_AL_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_AccessLogs PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_AL_company FOREIGN KEY (company_id) REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_AL_user    FOREIGN KEY (user_id)    REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT CK_ABASTA_AL_event CHECK (event IN
            ('login_success','login_failed','logout','logout_all','token_refresh',
             'signup','email_verified','password_reset','invitation_accepted'))
    );

    CREATE INDEX IX_ABASTA_AL_company ON dbo.ABASTA_AccessLogs(company_id, created_at DESC);
    CREATE INDEX IX_ABASTA_AL_user    ON dbo.ABASTA_AccessLogs(user_id, created_at DESC);
END
GO

PRINT 'Tabelas de acesso ABASTA (Invitations, AccessLogs) criadas.';
GO
