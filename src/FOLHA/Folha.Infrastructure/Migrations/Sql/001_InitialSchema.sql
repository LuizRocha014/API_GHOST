/*
    Folha — migration 001: cria todas as tabelas iniciais.
    Padrão de tabela: FOLHA_<EntityName> (PascalCase com prefixo)
    Colunas: snake_case
    O banco (FolhaDB) deve existir antes — a connection string aponta direto pra ele.
    Script idempotente: pode ser rodado múltiplas vezes sem erro.
*/

------------------------------------------------------------
-- FOLHA_Users
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Users
    (
        id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Users_id DEFAULT NEWSEQUENTIALID(),
        email           NVARCHAR(255)    NOT NULL,
        password_hash   NVARCHAR(255)    NOT NULL,
        full_name       NVARCHAR(150)    NOT NULL,
        display_name    NVARCHAR(60)     NOT NULL,
        avatar_url      NVARCHAR(500)    NULL,
        timezone        NVARCHAR(64)     NOT NULL CONSTRAINT DF_FOLHA_Users_timezone DEFAULT (N'America/Sao_Paulo'),
        locale          NVARCHAR(10)     NOT NULL CONSTRAINT DF_FOLHA_Users_locale   DEFAULT (N'pt-BR'),
        currency_code   CHAR(3)          NOT NULL CONSTRAINT DF_FOLHA_Users_currency DEFAULT ('BRL'),
        is_active       BIT              NOT NULL CONSTRAINT DF_FOLHA_Users_active   DEFAULT (1),
        email_verified  BIT              NOT NULL CONSTRAINT DF_FOLHA_Users_verified DEFAULT (0),
        last_login_at   DATETIME2(3)     NULL,
        created_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Users_created  DEFAULT (SYSUTCDATETIME()),
        updated_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Users_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Users PRIMARY KEY (id),
        CONSTRAINT UQ_FOLHA_Users_email UNIQUE (email),
        CONSTRAINT CK_FOLHA_Users_email_format CHECK (email LIKE N'%_@_%._%')
    );
END
GO

------------------------------------------------------------
-- FOLHA_Accounts
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Accounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Accounts
    (
        id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Accounts_id DEFAULT NEWSEQUENTIALID(),
        user_id           UNIQUEIDENTIFIER NOT NULL,
        name              NVARCHAR(80)     NOT NULL,
        kind              VARCHAR(20)      NOT NULL CONSTRAINT DF_FOLHA_Accounts_kind DEFAULT ('checking'),
        institution       NVARCHAR(80)     NULL,
        icon              NVARCHAR(40)     NULL,
        color_hex         CHAR(7)          NULL,
        initial_balance   DECIMAL(18,2)    NOT NULL CONSTRAINT DF_FOLHA_Accounts_initial DEFAULT (0),
        currency_code     CHAR(3)          NOT NULL CONSTRAINT DF_FOLHA_Accounts_currency DEFAULT ('BRL'),
        is_archived       BIT              NOT NULL CONSTRAINT DF_FOLHA_Accounts_archived DEFAULT (0),
        include_in_total  BIT              NOT NULL CONSTRAINT DF_FOLHA_Accounts_include  DEFAULT (1),
        sort_order        INT              NOT NULL CONSTRAINT DF_FOLHA_Accounts_sort     DEFAULT (0),
        created_at        DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Accounts_created  DEFAULT (SYSUTCDATETIME()),
        updated_at        DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Accounts_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Accounts PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Accounts_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT CK_FOLHA_Accounts_kind CHECK (kind IN ('checking','savings','cash','investment','other'))
    );

    CREATE INDEX IX_FOLHA_Accounts_user ON dbo.FOLHA_Accounts(user_id);
END
GO

------------------------------------------------------------
-- FOLHA_CreditCards
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_CreditCards', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_CreditCards
    (
        id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_CreditCards_id DEFAULT NEWSEQUENTIALID(),
        user_id       UNIQUEIDENTIFIER NOT NULL,
        account_id    UNIQUEIDENTIFIER NOT NULL,
        name          NVARCHAR(80)     NOT NULL,
        brand         NVARCHAR(20)     NOT NULL CONSTRAINT DF_FOLHA_CreditCards_brand DEFAULT ('other'),
        last_four     CHAR(4)          NULL,
        credit_limit  DECIMAL(18,2)    NOT NULL CONSTRAINT DF_FOLHA_CreditCards_limit DEFAULT (0),
        closing_day   TINYINT          NOT NULL,
        due_day       TINYINT          NOT NULL,
        is_archived   BIT              NOT NULL CONSTRAINT DF_FOLHA_CreditCards_archived DEFAULT (0),
        created_at    DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_CreditCards_created  DEFAULT (SYSUTCDATETIME()),
        updated_at    DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_CreditCards_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_CreditCards PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_CreditCards_user    FOREIGN KEY (user_id)    REFERENCES dbo.FOLHA_Users(id)    ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_CreditCards_account FOREIGN KEY (account_id) REFERENCES dbo.FOLHA_Accounts(id),
        CONSTRAINT CK_FOLHA_CreditCards_brand   CHECK (brand IN ('visa','master','amex','elo','hiper','other')),
        CONSTRAINT CK_FOLHA_CreditCards_closing CHECK (closing_day BETWEEN 1 AND 31),
        CONSTRAINT CK_FOLHA_CreditCards_due     CHECK (due_day BETWEEN 1 AND 31)
    );

    CREATE INDEX IX_FOLHA_CreditCards_user ON dbo.FOLHA_CreditCards(user_id);
END
GO

------------------------------------------------------------
-- FOLHA_CreditCardStatements
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_CreditCardStatements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_CreditCardStatements
    (
        id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_CCS_id DEFAULT NEWSEQUENTIALID(),
        credit_card_id        UNIQUEIDENTIFIER NOT NULL,
        reference_month       DATE             NOT NULL,
        closing_date          DATE             NOT NULL,
        due_date              DATE             NOT NULL,
        total_amount          DECIMAL(18,2)    NOT NULL CONSTRAINT DF_FOLHA_CCS_total DEFAULT (0),
        paid_amount           DECIMAL(18,2)    NOT NULL CONSTRAINT DF_FOLHA_CCS_paid  DEFAULT (0),
        status                VARCHAR(20)      NOT NULL CONSTRAINT DF_FOLHA_CCS_status DEFAULT ('open'),
        paid_at               DATETIME2(3)     NULL,
        paid_from_account_id  UNIQUEIDENTIFIER NULL,
        created_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_CCS_created DEFAULT (SYSUTCDATETIME()),
        updated_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_CCS_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_CCS PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_CCS_card    FOREIGN KEY (credit_card_id)       REFERENCES dbo.FOLHA_CreditCards(id) ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_CCS_paidacc FOREIGN KEY (paid_from_account_id) REFERENCES dbo.FOLHA_Accounts(id),
        CONSTRAINT UQ_FOLHA_CCS_card_month UNIQUE (credit_card_id, reference_month),
        CONSTRAINT CK_FOLHA_CCS_status CHECK (status IN ('open','closed','paid','partial','overdue'))
    );
END
GO

------------------------------------------------------------
-- FOLHA_Categories  (INT IDENTITY; user_id NULL = sistema)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Categories
    (
        id           INT              NOT NULL IDENTITY(1,1),
        user_id      UNIQUEIDENTIFIER NULL,
        slug         NVARCHAR(40)     NOT NULL,
        label        NVARCHAR(60)     NOT NULL,
        icon         NVARCHAR(40)     NULL,
        color_hex    CHAR(7)          NULL,
        bg_hex       CHAR(7)          NULL,
        kind         VARCHAR(10)      NOT NULL CONSTRAINT DF_FOLHA_Categories_kind DEFAULT ('expense'),
        is_system    BIT              NOT NULL CONSTRAINT DF_FOLHA_Categories_system DEFAULT (0),
        is_archived  BIT              NOT NULL CONSTRAINT DF_FOLHA_Categories_archived DEFAULT (0),
        sort_order   INT              NOT NULL CONSTRAINT DF_FOLHA_Categories_sort DEFAULT (0),
        created_at   DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Categories_created DEFAULT (SYSUTCDATETIME()),
        updated_at   DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Categories_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Categories PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Categories_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT CK_FOLHA_Categories_kind CHECK (kind IN ('expense','income','both')),
        CONSTRAINT CK_FOLHA_Categories_color_hex CHECK (color_hex IS NULL OR color_hex LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
        CONSTRAINT CK_FOLHA_Categories_bg_hex    CHECK (bg_hex    IS NULL OR bg_hex    LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]')
    );

    -- slug único por usuário (incluindo as do sistema, com user_id NULL como “grupo único”)
    CREATE UNIQUE INDEX UX_FOLHA_Categories_system_slug ON dbo.FOLHA_Categories(slug) WHERE user_id IS NULL;
    CREATE UNIQUE INDEX UX_FOLHA_Categories_user_slug   ON dbo.FOLHA_Categories(user_id, slug) WHERE user_id IS NOT NULL;
END
GO

------------------------------------------------------------
-- FOLHA_Recurrences (precisa existir antes de Bills/Transactions)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Recurrences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Recurrences
    (
        id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Recurrences_id DEFAULT NEWSEQUENTIALID(),
        user_id            UNIQUEIDENTIFIER NOT NULL,
        frequency          VARCHAR(10)      NOT NULL CONSTRAINT DF_FOLHA_Recurrences_freq DEFAULT ('monthly'),
        every_n            INT              NOT NULL CONSTRAINT DF_FOLHA_Recurrences_every DEFAULT (1),
        day_of_month       TINYINT          NULL,
        day_of_week        TINYINT          NULL,
        month_of_year      TINYINT          NULL,
        start_date         DATE             NOT NULL,
        end_date           DATE             NULL,
        max_occurrences    INT              NULL,
        last_generated_at  DATETIME2(3)     NULL,
        created_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Recurrences_created DEFAULT (SYSUTCDATETIME()),
        updated_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Recurrences_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Recurrences PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Recurrences_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT CK_FOLHA_Recurrences_freq CHECK (frequency IN ('daily','weekly','monthly','yearly')),
        CONSTRAINT CK_FOLHA_Recurrences_every CHECK (every_n >= 1)
    );
END
GO

------------------------------------------------------------
-- FOLHA_Transfers (header)  — antes de Transactions p/ FK opcional
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Transfers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Transfers
    (
        id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Transfers_id DEFAULT NEWSEQUENTIALID(),
        user_id          UNIQUEIDENTIFIER NOT NULL,
        from_account_id  UNIQUEIDENTIFIER NOT NULL,
        to_account_id    UNIQUEIDENTIFIER NOT NULL,
        amount           DECIMAL(18,2)    NOT NULL,
        fee              DECIMAL(18,2)    NOT NULL CONSTRAINT DF_FOLHA_Transfers_fee DEFAULT (0),
        occurred_at      DATETIME2(3)     NOT NULL,
        description      NVARCHAR(150)    NULL,
        notes            NVARCHAR(1000)   NULL,
        created_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Transfers_created DEFAULT (SYSUTCDATETIME()),
        updated_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Transfers_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Transfers PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Transfers_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_Transfers_from FOREIGN KEY (from_account_id) REFERENCES dbo.FOLHA_Accounts(id),
        CONSTRAINT FK_FOLHA_Transfers_to   FOREIGN KEY (to_account_id)   REFERENCES dbo.FOLHA_Accounts(id),
        CONSTRAINT CK_FOLHA_Transfers_amount CHECK (amount > 0),
        CONSTRAINT CK_FOLHA_Transfers_fee    CHECK (fee >= 0),
        CONSTRAINT CK_FOLHA_Transfers_diff   CHECK (from_account_id <> to_account_id)
    );
END
GO

------------------------------------------------------------
-- FOLHA_Bills
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Bills', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Bills
    (
        id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Bills_id DEFAULT NEWSEQUENTIALID(),
        user_id               UNIQUEIDENTIFIER NOT NULL,
        account_id            UNIQUEIDENTIFIER NULL,
        category_id           INT              NULL,
        recurrence_id         UNIQUEIDENTIFIER NULL,
        description           NVARCHAR(150)    NOT NULL,
        amount                DECIMAL(18,2)    NOT NULL,
        kind                  VARCHAR(10)      NOT NULL CONSTRAINT DF_FOLHA_Bills_kind DEFAULT ('payable'),
        due_date              DATE             NOT NULL,
        status                VARCHAR(15)      NOT NULL CONSTRAINT DF_FOLHA_Bills_status DEFAULT ('pending'),
        paid_at               DATETIME2(3)     NULL,
        paid_transaction_id   UNIQUEIDENTIFIER NULL,
        notes                 NVARCHAR(500)    NULL,
        created_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Bills_created DEFAULT (SYSUTCDATETIME()),
        updated_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Bills_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Bills PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Bills_user       FOREIGN KEY (user_id)       REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_Bills_account    FOREIGN KEY (account_id)    REFERENCES dbo.FOLHA_Accounts(id),
        CONSTRAINT FK_FOLHA_Bills_category   FOREIGN KEY (category_id)   REFERENCES dbo.FOLHA_Categories(id),
        CONSTRAINT FK_FOLHA_Bills_recurrence FOREIGN KEY (recurrence_id) REFERENCES dbo.FOLHA_Recurrences(id),
        CONSTRAINT CK_FOLHA_Bills_amount CHECK (amount > 0),
        CONSTRAINT CK_FOLHA_Bills_kind   CHECK (kind IN ('payable','receivable')),
        CONSTRAINT CK_FOLHA_Bills_status CHECK (status IN ('pending','paid','received','overdue','cancelled'))
    );

    CREATE INDEX IX_FOLHA_Bills_user_due ON dbo.FOLHA_Bills(user_id, due_date);
END
GO

------------------------------------------------------------
-- FOLHA_Transactions  (mais importante)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Transactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Transactions
    (
        id                         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Tx_id DEFAULT NEWSEQUENTIALID(),
        user_id                    UNIQUEIDENTIFIER NOT NULL,
        account_id                 UNIQUEIDENTIFIER NULL,
        credit_card_id             UNIQUEIDENTIFIER NULL,
        credit_card_statement_id   UNIQUEIDENTIFIER NULL,
        category_id                INT              NOT NULL,
        bill_id                    UNIQUEIDENTIFIER NULL,
        recurrence_id              UNIQUEIDENTIFIER NULL,
        parent_transaction_id      UNIQUEIDENTIFIER NULL,
        transfer_id                UNIQUEIDENTIFIER NULL,
        description                NVARCHAR(150)    NOT NULL,
        place                      NVARCHAR(120)    NULL,
        notes                      NVARCHAR(1000)   NULL,
        amount                     DECIMAL(18,2)    NOT NULL,
        kind                       VARCHAR(15)      NOT NULL,
        occurred_at                DATETIME2(3)     NOT NULL,
        installment_number         SMALLINT         NULL,
        installment_total          SMALLINT         NULL,
        is_pending                 BIT              NOT NULL CONSTRAINT DF_FOLHA_Tx_pending  DEFAULT (0),
        is_excluded_from_reports   BIT              NOT NULL CONSTRAINT DF_FOLHA_Tx_excluded DEFAULT (0),
        deleted_at                 DATETIME2(3)     NULL,
        created_at                 DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Tx_created  DEFAULT (SYSUTCDATETIME()),
        updated_at                 DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Tx_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Transactions PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Tx_user       FOREIGN KEY (user_id)                  REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_Tx_account    FOREIGN KEY (account_id)               REFERENCES dbo.FOLHA_Accounts(id),
        CONSTRAINT FK_FOLHA_Tx_card       FOREIGN KEY (credit_card_id)           REFERENCES dbo.FOLHA_CreditCards(id),
        CONSTRAINT FK_FOLHA_Tx_statement  FOREIGN KEY (credit_card_statement_id) REFERENCES dbo.FOLHA_CreditCardStatements(id),
        CONSTRAINT FK_FOLHA_Tx_category   FOREIGN KEY (category_id)              REFERENCES dbo.FOLHA_Categories(id),
        CONSTRAINT FK_FOLHA_Tx_bill       FOREIGN KEY (bill_id)                  REFERENCES dbo.FOLHA_Bills(id),
        CONSTRAINT FK_FOLHA_Tx_recurrence FOREIGN KEY (recurrence_id)            REFERENCES dbo.FOLHA_Recurrences(id),
        CONSTRAINT FK_FOLHA_Tx_parent     FOREIGN KEY (parent_transaction_id)    REFERENCES dbo.FOLHA_Transactions(id),
        CONSTRAINT FK_FOLHA_Tx_transfer   FOREIGN KEY (transfer_id)              REFERENCES dbo.FOLHA_Transfers(id),
        CONSTRAINT CK_FOLHA_Tx_amount    CHECK (amount > 0),
        CONSTRAINT CK_FOLHA_Tx_kind      CHECK (kind IN ('expense','income','transfer_out','transfer_in')),
        CONSTRAINT CK_FOLHA_Tx_target    CHECK (account_id IS NOT NULL OR credit_card_id IS NOT NULL),
        CONSTRAINT CK_FOLHA_Tx_install   CHECK (
            (installment_number IS NULL AND installment_total IS NULL)
            OR (installment_number BETWEEN 1 AND installment_total)
        )
    );

    CREATE INDEX IX_FOLHA_Tx_user_occurred ON dbo.FOLHA_Transactions(user_id, occurred_at DESC) WHERE deleted_at IS NULL;
END
GO

------------------------------------------------------------
-- FOLHA_Goals
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Goals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Goals
    (
        id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Goals_id DEFAULT NEWSEQUENTIALID(),
        user_id         UNIQUEIDENTIFIER NOT NULL,
        account_id      UNIQUEIDENTIFIER NULL,
        title           NVARCHAR(120)    NOT NULL,
        description     NVARCHAR(500)    NULL,
        target_amount   DECIMAL(18,2)    NOT NULL,
        current_amount  DECIMAL(18,2)    NOT NULL CONSTRAINT DF_FOLHA_Goals_current DEFAULT (0),
        target_date     DATE             NULL,
        icon            NVARCHAR(40)     NULL,
        color_hex       CHAR(7)          NULL,
        is_completed    BIT              NOT NULL CONSTRAINT DF_FOLHA_Goals_completed DEFAULT (0),
        completed_at    DATETIME2(3)     NULL,
        is_archived     BIT              NOT NULL CONSTRAINT DF_FOLHA_Goals_archived  DEFAULT (0),
        created_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Goals_created   DEFAULT (SYSUTCDATETIME()),
        updated_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Goals_updated   DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Goals PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Goals_user    FOREIGN KEY (user_id)    REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_Goals_account FOREIGN KEY (account_id) REFERENCES dbo.FOLHA_Accounts(id),
        CONSTRAINT CK_FOLHA_Goals_target  CHECK (target_amount > 0)
    );
END
GO

------------------------------------------------------------
-- FOLHA_GoalContributions
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_GoalContributions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_GoalContributions
    (
        id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_GC_id DEFAULT NEWSEQUENTIALID(),
        goal_id         UNIQUEIDENTIFIER NOT NULL,
        transaction_id  UNIQUEIDENTIFIER NULL,
        amount          DECIMAL(18,2)    NOT NULL,
        contributed_at  DATETIME2(3)     NOT NULL,
        note            NVARCHAR(300)    NULL,
        created_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_GC_created DEFAULT (SYSUTCDATETIME()),
        updated_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_GC_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_GoalContributions PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_GC_goal FOREIGN KEY (goal_id)        REFERENCES dbo.FOLHA_Goals(id) ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_GC_tx   FOREIGN KEY (transaction_id) REFERENCES dbo.FOLHA_Transactions(id)
    );
END
GO

------------------------------------------------------------
-- FOLHA_Budgets
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Budgets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Budgets
    (
        id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Budgets_id DEFAULT NEWSEQUENTIALID(),
        user_id          UNIQUEIDENTIFIER NOT NULL,
        category_id      INT              NOT NULL,
        reference_month  DATE             NOT NULL,
        amount_limit     DECIMAL(18,2)    NOT NULL,
        rollover         BIT              NOT NULL CONSTRAINT DF_FOLHA_Budgets_rollover DEFAULT (0),
        alert_threshold  TINYINT          NOT NULL CONSTRAINT DF_FOLHA_Budgets_alert    DEFAULT (80),
        created_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Budgets_created  DEFAULT (SYSUTCDATETIME()),
        updated_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Budgets_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Budgets PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Budgets_user     FOREIGN KEY (user_id)     REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT FK_FOLHA_Budgets_category FOREIGN KEY (category_id) REFERENCES dbo.FOLHA_Categories(id),
        CONSTRAINT UQ_FOLHA_Budgets_uc UNIQUE (user_id, category_id, reference_month),
        CONSTRAINT CK_FOLHA_Budgets_limit CHECK (amount_limit > 0),
        CONSTRAINT CK_FOLHA_Budgets_alert CHECK (alert_threshold BETWEEN 0 AND 100)
    );
END
GO

------------------------------------------------------------
-- FOLHA_Notifications
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Notifications
    (
        id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Notif_id DEFAULT NEWSEQUENTIALID(),
        user_id        UNIQUEIDENTIFIER NOT NULL,
        kind           VARCHAR(30)      NOT NULL CONSTRAINT DF_FOLHA_Notif_kind DEFAULT ('system'),
        title          NVARCHAR(150)    NOT NULL,
        body           NVARCHAR(1000)   NULL,
        related_kind   NVARCHAR(40)     NULL,
        related_id     NVARCHAR(64)     NULL,
        scheduled_for  DATETIME2(3)     NULL,
        is_read        BIT              NOT NULL CONSTRAINT DF_FOLHA_Notif_read DEFAULT (0),
        read_at        DATETIME2(3)     NULL,
        created_at     DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Notif_created DEFAULT (SYSUTCDATETIME()),
        updated_at     DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Notif_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Notifications PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Notif_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_FOLHA_Notif_user_read ON dbo.FOLHA_Notifications(user_id, is_read, created_at DESC);
END
GO

------------------------------------------------------------
-- FOLHA_Sessions
------------------------------------------------------------
IF OBJECT_ID(N'dbo.FOLHA_Sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FOLHA_Sessions
    (
        id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FOLHA_Sessions_id DEFAULT NEWSEQUENTIALID(),
        user_id     UNIQUEIDENTIFIER NOT NULL,
        token_hash  NVARCHAR(255)    NOT NULL,
        user_agent  NVARCHAR(500)    NULL,
        ip_address  NVARCHAR(45)     NULL,
        expires_at  DATETIME2(3)     NOT NULL,
        revoked_at  DATETIME2(3)     NULL,
        created_at  DATETIME2(3)     NOT NULL CONSTRAINT DF_FOLHA_Sessions_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FOLHA_Sessions PRIMARY KEY (id),
        CONSTRAINT FK_FOLHA_Sessions_user FOREIGN KEY (user_id) REFERENCES dbo.FOLHA_Users(id) ON DELETE CASCADE,
        CONSTRAINT UQ_FOLHA_Sessions_token UNIQUE (token_hash)
    );
END
GO

PRINT 'Schema FOLHA_* criado/atualizado com sucesso.';
GO
