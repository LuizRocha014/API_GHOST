/*
    Abasta — migration 001: cria todo o schema inicial.

    Domínio: controle de gastos com combustível para empresas e frotas.
    Tenant raiz: ABASTA_Companies (toda informação de negócio é escopada por company_id).

    Convenções (iguais às demais APIs da solução):
      - Tabela: ABASTA_<EntityName>  (PascalCase com prefixo ABASTA_)
      - Colunas: snake_case
      - PK GUID: UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID() (sequencial -> índices saudáveis em alto volume)
      - Lookups pequenos: INT IDENTITY
      - created_at / updated_at: DATETIME2(3) DEFAULT SYSUTCDATETIME()
      - Dinheiro: DECIMAL(18,2) · litros/preço: DECIMAL(10,3) · hodômetro: INT

    Escalabilidade:
      - Tabela quente (ABASTA_FuelEntries) com soft delete (deleted_at) e índices
        compostos filtrados pelos padrões de consulta (empresa/usuário/veículo + data).
      - CASCADE só em tabelas de sessão/notificação a partir de ABASTA_Users
        (caminho único). Tabelas de negócio usam NO ACTION + soft delete, evitando
        múltiplos caminhos de cascade no SQL Server e exclusões em massa acidentais.

    Script idempotente: pode ser executado múltiplas vezes sem erro.
*/

------------------------------------------------------------
-- ABASTA_Companies  (tenant raiz)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Companies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Companies
    (
        id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Companies_id DEFAULT NEWSEQUENTIALID(),
        name            NVARCHAR(150)    NOT NULL,
        legal_name      NVARCHAR(200)    NULL,
        cnpj            CHAR(14)         NULL,
        monthly_budget  DECIMAL(18,2)    NULL,
        timezone        NVARCHAR(64)     NOT NULL CONSTRAINT DF_ABASTA_Companies_tz       DEFAULT (N'America/Sao_Paulo'),
        currency_code   CHAR(3)          NOT NULL CONSTRAINT DF_ABASTA_Companies_currency DEFAULT ('BRL'),
        is_active       BIT              NOT NULL CONSTRAINT DF_ABASTA_Companies_active   DEFAULT (1),
        created_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Companies_created  DEFAULT (SYSUTCDATETIME()),
        updated_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Companies_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Companies PRIMARY KEY (id),
        CONSTRAINT CK_ABASTA_Companies_cnpj CHECK (cnpj IS NULL OR cnpj NOT LIKE '%[^0-9]%')
    );

    CREATE UNIQUE INDEX UX_ABASTA_Companies_cnpj ON dbo.ABASTA_Companies(cnpj) WHERE cnpj IS NOT NULL;
END
GO

------------------------------------------------------------
-- ABASTA_FuelTypes  (lookup; INT IDENTITY)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_FuelTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_FuelTypes
    (
        id          INT          NOT NULL IDENTITY(1,1),
        code        VARCHAR(20)  NOT NULL,
        label       NVARCHAR(40) NOT NULL,
        color_hex   CHAR(7)      NULL,
        sort_order  INT          NOT NULL CONSTRAINT DF_ABASTA_FuelTypes_sort   DEFAULT (0),
        is_active   BIT          NOT NULL CONSTRAINT DF_ABASTA_FuelTypes_active DEFAULT (1),
        created_at  DATETIME2(3) NOT NULL CONSTRAINT DF_ABASTA_FuelTypes_created DEFAULT (SYSUTCDATETIME()),
        updated_at  DATETIME2(3) NOT NULL CONSTRAINT DF_ABASTA_FuelTypes_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_FuelTypes PRIMARY KEY (id),
        CONSTRAINT UQ_ABASTA_FuelTypes_code UNIQUE (code),
        CONSTRAINT CK_ABASTA_FuelTypes_color CHECK (color_hex IS NULL OR color_hex LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]')
    );
END
GO

------------------------------------------------------------
-- ABASTA_FuelAccounts  (contas da empresa em distribuidoras — "conta na BR Distribuidora")
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_FuelAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_FuelAccounts
    (
        id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_FuelAccounts_id DEFAULT NEWSEQUENTIALID(),
        company_id      UNIQUEIDENTIFIER NOT NULL,
        distributor     NVARCHAR(80)     NOT NULL,
        account_number  NVARCHAR(60)     NULL,
        is_active       BIT              NOT NULL CONSTRAINT DF_ABASTA_FuelAccounts_active  DEFAULT (1),
        created_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_FuelAccounts_created DEFAULT (SYSUTCDATETIME()),
        updated_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_FuelAccounts_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_FuelAccounts PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_FuelAccounts_company FOREIGN KEY (company_id) REFERENCES dbo.ABASTA_Companies(id)
    );

    CREATE INDEX IX_ABASTA_FuelAccounts_company ON dbo.ABASTA_FuelAccounts(company_id);
END
GO

------------------------------------------------------------
-- ABASTA_Users  (colaboradores e gestores)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Users
    (
        id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Users_id DEFAULT NEWSEQUENTIALID(),
        company_id      UNIQUEIDENTIFIER NOT NULL,
        email           NVARCHAR(255)    NOT NULL,
        password_hash   NVARCHAR(255)    NOT NULL,
        full_name       NVARCHAR(150)    NOT NULL,
        display_name    NVARCHAR(60)     NOT NULL,
        role            VARCHAR(20)      NOT NULL CONSTRAINT DF_ABASTA_Users_role     DEFAULT ('collaborator'),
        job_title       NVARCHAR(80)     NULL,
        phone           NVARCHAR(20)     NULL,
        avatar_url      NVARCHAR(500)    NULL,
        is_active       BIT              NOT NULL CONSTRAINT DF_ABASTA_Users_active   DEFAULT (1),
        email_verified  BIT              NOT NULL CONSTRAINT DF_ABASTA_Users_verified DEFAULT (0),
        last_login_at   DATETIME2(3)     NULL,
        created_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Users_created  DEFAULT (SYSUTCDATETIME()),
        updated_at      DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Users_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Users PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Users_company FOREIGN KEY (company_id) REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT UQ_ABASTA_Users_email UNIQUE (email),
        CONSTRAINT CK_ABASTA_Users_role  CHECK (role IN ('collaborator','admin')),
        CONSTRAINT CK_ABASTA_Users_email_format CHECK (email LIKE N'%_@_%._%')
    );

    CREATE INDEX IX_ABASTA_Users_company ON dbo.ABASTA_Users(company_id);
END
GO

------------------------------------------------------------
-- ABASTA_Sessions
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Sessions
    (
        id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Sessions_id DEFAULT NEWSEQUENTIALID(),
        user_id     UNIQUEIDENTIFIER NOT NULL,
        token_hash  NVARCHAR(255)    NOT NULL,
        user_agent  NVARCHAR(500)    NULL,
        ip_address  NVARCHAR(45)     NULL,
        expires_at  DATETIME2(3)     NOT NULL,
        revoked_at  DATETIME2(3)     NULL,
        created_at  DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Sessions_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Sessions PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Sessions_user FOREIGN KEY (user_id) REFERENCES dbo.ABASTA_Users(id) ON DELETE CASCADE,
        CONSTRAINT UQ_ABASTA_Sessions_token UNIQUE (token_hash)
    );

    CREATE INDEX IX_ABASTA_Sessions_user ON dbo.ABASTA_Sessions(user_id);
END
GO

------------------------------------------------------------
-- ABASTA_RefreshTokens
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_RefreshTokens
    (
        id                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_RT_id DEFAULT NEWSEQUENTIALID(),
        user_id                 UNIQUEIDENTIFIER NOT NULL,
        token_hash              NVARCHAR(255)    NOT NULL,
        device_id               NVARCHAR(100)    NULL,
        user_agent              NVARCHAR(500)    NULL,
        ip_address              NVARCHAR(45)     NULL,
        expires_at              DATETIME2(3)     NOT NULL,
        revoked_at              DATETIME2(3)     NULL,
        replaced_by_token_id    UNIQUEIDENTIFIER NULL,
        created_at              DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_RT_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_RefreshTokens PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_RT_user FOREIGN KEY (user_id) REFERENCES dbo.ABASTA_Users(id) ON DELETE CASCADE,
        CONSTRAINT UQ_ABASTA_RT_token UNIQUE (token_hash)
    );

    CREATE INDEX IX_ABASTA_RT_user ON dbo.ABASTA_RefreshTokens(user_id);
END
GO

------------------------------------------------------------
-- ABASTA_EmailVerifications  (OTP de cadastro/recuperação)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_EmailVerifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_EmailVerifications
    (
        id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_EV_id DEFAULT NEWSEQUENTIALID(),
        user_id     UNIQUEIDENTIFIER NOT NULL,
        email       NVARCHAR(255)    NOT NULL,
        code_hash   NVARCHAR(255)    NOT NULL,
        purpose     VARCHAR(20)      NOT NULL CONSTRAINT DF_ABASTA_EV_purpose DEFAULT ('signup'),
        attempts    INT              NOT NULL CONSTRAINT DF_ABASTA_EV_attempts DEFAULT (0),
        expires_at  DATETIME2(3)     NOT NULL,
        consumed_at DATETIME2(3)     NULL,
        created_at  DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_EV_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_EmailVerifications PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_EV_user FOREIGN KEY (user_id) REFERENCES dbo.ABASTA_Users(id) ON DELETE CASCADE,
        CONSTRAINT CK_ABASTA_EV_purpose CHECK (purpose IN ('signup','reset','email_change'))
    );

    CREATE INDEX IX_ABASTA_EV_user ON dbo.ABASTA_EmailVerifications(user_id, created_at DESC);
END
GO

------------------------------------------------------------
-- ABASTA_NotificationPreferences  (1:1 com usuário)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_NotificationPreferences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_NotificationPreferences
    (
        user_id               UNIQUEIDENTIFIER NOT NULL,
        new_receipt_enabled   BIT              NOT NULL CONSTRAINT DF_ABASTA_NP_receipt DEFAULT (1),
        weekly_report_enabled BIT              NOT NULL CONSTRAINT DF_ABASTA_NP_weekly  DEFAULT (1),
        over_budget_enabled   BIT              NOT NULL CONSTRAINT DF_ABASTA_NP_budget  DEFAULT (1),
        updated_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_NP_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_NotificationPreferences PRIMARY KEY (user_id),
        CONSTRAINT FK_ABASTA_NP_user FOREIGN KEY (user_id) REFERENCES dbo.ABASTA_Users(id) ON DELETE CASCADE
    );
END
GO

------------------------------------------------------------
-- ABASTA_Vehicles  (frota)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Vehicles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Vehicles
    (
        id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Vehicles_id DEFAULT NEWSEQUENTIALID(),
        company_id            UNIQUEIDENTIFIER NOT NULL,
        fuel_account_id       UNIQUEIDENTIFIER NULL,
        default_fuel_type_id  INT              NULL,
        plate                 VARCHAR(8)       NOT NULL,
        label                 NVARCHAR(80)     NULL,
        make                  NVARCHAR(40)     NULL,
        model                 NVARCHAR(40)     NULL,
        model_year            SMALLINT         NULL,
        tank_capacity_liters  DECIMAL(10,3)    NULL,
        current_odometer_km   INT              NULL,
        monthly_budget        DECIMAL(18,2)    NULL,
        is_active             BIT              NOT NULL CONSTRAINT DF_ABASTA_Vehicles_active  DEFAULT (1),
        created_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Vehicles_created DEFAULT (SYSUTCDATETIME()),
        updated_at            DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Vehicles_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Vehicles PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Vehicles_company   FOREIGN KEY (company_id)           REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_Vehicles_account   FOREIGN KEY (fuel_account_id)      REFERENCES dbo.ABASTA_FuelAccounts(id),
        CONSTRAINT FK_ABASTA_Vehicles_fueltype  FOREIGN KEY (default_fuel_type_id) REFERENCES dbo.ABASTA_FuelTypes(id),
        CONSTRAINT CK_ABASTA_Vehicles_odometer  CHECK (current_odometer_km IS NULL OR current_odometer_km >= 0),
        CONSTRAINT CK_ABASTA_Vehicles_tank      CHECK (tank_capacity_liters IS NULL OR tank_capacity_liters > 0)
    );

    CREATE UNIQUE INDEX UX_ABASTA_Vehicles_company_plate ON dbo.ABASTA_Vehicles(company_id, plate);
    CREATE INDEX        IX_ABASTA_Vehicles_company       ON dbo.ABASTA_Vehicles(company_id);
END
GO

------------------------------------------------------------
-- ABASTA_VehicleAssignments  (histórico de quem dirige cada veículo)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_VehicleAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_VehicleAssignments
    (
        id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_VA_id DEFAULT NEWSEQUENTIALID(),
        company_id     UNIQUEIDENTIFIER NOT NULL,
        vehicle_id     UNIQUEIDENTIFIER NOT NULL,
        user_id        UNIQUEIDENTIFIER NOT NULL,
        is_primary     BIT              NOT NULL CONSTRAINT DF_ABASTA_VA_primary DEFAULT (1),
        assigned_at    DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_VA_assigned DEFAULT (SYSUTCDATETIME()),
        unassigned_at  DATETIME2(3)     NULL,
        created_at     DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_VA_created DEFAULT (SYSUTCDATETIME()),
        updated_at     DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_VA_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_VehicleAssignments PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_VA_company FOREIGN KEY (company_id) REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_VA_vehicle FOREIGN KEY (vehicle_id) REFERENCES dbo.ABASTA_Vehicles(id),
        CONSTRAINT FK_ABASTA_VA_user    FOREIGN KEY (user_id)    REFERENCES dbo.ABASTA_Users(id)
    );

    CREATE INDEX IX_ABASTA_VA_vehicle ON dbo.ABASTA_VehicleAssignments(vehicle_id);
    CREATE INDEX IX_ABASTA_VA_user    ON dbo.ABASTA_VehicleAssignments(user_id);
    -- No máximo um motorista primário ativo por veículo.
    CREATE UNIQUE INDEX UX_ABASTA_VA_primary_active
        ON dbo.ABASTA_VehicleAssignments(vehicle_id)
        WHERE unassigned_at IS NULL AND is_primary = 1;
END
GO

------------------------------------------------------------
-- ABASTA_Stations  (postos)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Stations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Stations
    (
        id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Stations_id DEFAULT NEWSEQUENTIALID(),
        company_id  UNIQUEIDENTIFIER NOT NULL,
        name        NVARCHAR(120)    NOT NULL,
        brand       NVARCHAR(40)     NULL,
        cnpj        CHAR(14)         NULL,
        city        NVARCHAR(80)     NULL,
        state       CHAR(2)          NULL,
        latitude    DECIMAL(9,6)     NULL,
        longitude   DECIMAL(9,6)     NULL,
        is_active   BIT              NOT NULL CONSTRAINT DF_ABASTA_Stations_active  DEFAULT (1),
        created_at  DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Stations_created DEFAULT (SYSUTCDATETIME()),
        updated_at  DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Stations_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Stations PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Stations_company FOREIGN KEY (company_id) REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT CK_ABASTA_Stations_cnpj CHECK (cnpj IS NULL OR cnpj NOT LIKE '%[^0-9]%')
    );

    CREATE UNIQUE INDEX UX_ABASTA_Stations_company_name ON dbo.ABASTA_Stations(company_id, name);
    CREATE INDEX        IX_ABASTA_Stations_company      ON dbo.ABASTA_Stations(company_id);
END
GO

------------------------------------------------------------
-- ABASTA_Receipts  (foto do cupom + OCR)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Receipts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Receipts
    (
        id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Receipts_id DEFAULT NEWSEQUENTIALID(),
        company_id          UNIQUEIDENTIFIER NOT NULL,
        uploaded_by_user_id UNIQUEIDENTIFIER NOT NULL,
        storage_url         NVARCHAR(500)    NOT NULL,
        content_type        NVARCHAR(60)     NULL,
        byte_size           BIGINT           NULL,
        ocr_status          VARCHAR(20)      NOT NULL CONSTRAINT DF_ABASTA_Receipts_ocr DEFAULT ('pending'),
        ocr_confidence      DECIMAL(5,2)     NULL,
        ocr_raw_json        NVARCHAR(MAX)    NULL,
        created_at          DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Receipts_created DEFAULT (SYSUTCDATETIME()),
        updated_at          DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Receipts_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Receipts PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Receipts_company FOREIGN KEY (company_id)          REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_Receipts_user    FOREIGN KEY (uploaded_by_user_id) REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT CK_ABASTA_Receipts_ocr CHECK (ocr_status IN ('pending','processing','done','failed'))
    );

    CREATE INDEX IX_ABASTA_Receipts_company ON dbo.ABASTA_Receipts(company_id, created_at DESC);
END
GO

------------------------------------------------------------
-- ABASTA_FuelEntries  (nota de abastecimento — tabela quente)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_FuelEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_FuelEntries
    (
        id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_FE_id DEFAULT NEWSEQUENTIALID(),
        company_id       UNIQUEIDENTIFIER NOT NULL,
        user_id          UNIQUEIDENTIFIER NOT NULL,
        vehicle_id       UNIQUEIDENTIFIER NOT NULL,
        fuel_type_id     INT              NOT NULL,
        station_id       UNIQUEIDENTIFIER NULL,
        receipt_id       UNIQUEIDENTIFIER NULL,
        station_name     NVARCHAR(120)    NULL,
        liters           DECIMAL(10,3)    NOT NULL,
        price_per_liter  DECIMAL(10,3)    NOT NULL,
        total_amount     DECIMAL(18,2)    NOT NULL,
        odometer_km      INT              NULL,
        fueled_at        DATETIME2(3)     NOT NULL,
        status           VARCHAR(20)      NOT NULL CONSTRAINT DF_ABASTA_FE_status DEFAULT ('registered'),
        source           VARCHAR(15)      NOT NULL CONSTRAINT DF_ABASTA_FE_source DEFAULT ('manual'),
        notes            NVARCHAR(500)    NULL,
        deleted_at       DATETIME2(3)     NULL,
        created_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_FE_created DEFAULT (SYSUTCDATETIME()),
        updated_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_FE_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_FuelEntries PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_FE_company  FOREIGN KEY (company_id)   REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_FE_user     FOREIGN KEY (user_id)      REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT FK_ABASTA_FE_vehicle  FOREIGN KEY (vehicle_id)   REFERENCES dbo.ABASTA_Vehicles(id),
        CONSTRAINT FK_ABASTA_FE_fueltype FOREIGN KEY (fuel_type_id) REFERENCES dbo.ABASTA_FuelTypes(id),
        CONSTRAINT FK_ABASTA_FE_station  FOREIGN KEY (station_id)   REFERENCES dbo.ABASTA_Stations(id),
        CONSTRAINT FK_ABASTA_FE_receipt  FOREIGN KEY (receipt_id)   REFERENCES dbo.ABASTA_Receipts(id),
        CONSTRAINT CK_ABASTA_FE_liters   CHECK (liters > 0),
        CONSTRAINT CK_ABASTA_FE_price    CHECK (price_per_liter >= 0),
        CONSTRAINT CK_ABASTA_FE_total    CHECK (total_amount > 0),
        CONSTRAINT CK_ABASTA_FE_odometer CHECK (odometer_km IS NULL OR odometer_km >= 0),
        CONSTRAINT CK_ABASTA_FE_status   CHECK (status IN ('registered','pending_review','rejected')),
        CONSTRAINT CK_ABASTA_FE_source   CHECK (source IN ('manual','ocr','import'))
    );

    -- Padrões de consulta (escala): filtrados por soft delete.
    CREATE INDEX IX_ABASTA_FE_company_fueled ON dbo.ABASTA_FuelEntries(company_id, fueled_at DESC) WHERE deleted_at IS NULL;
    CREATE INDEX IX_ABASTA_FE_user_fueled    ON dbo.ABASTA_FuelEntries(user_id,    fueled_at DESC) WHERE deleted_at IS NULL;
    CREATE INDEX IX_ABASTA_FE_vehicle_fueled ON dbo.ABASTA_FuelEntries(vehicle_id, fueled_at DESC) WHERE deleted_at IS NULL;
    CREATE INDEX IX_ABASTA_FE_station        ON dbo.ABASTA_FuelEntries(station_id)                 WHERE deleted_at IS NULL;
    CREATE INDEX IX_ABASTA_FE_fueltype       ON dbo.ABASTA_FuelEntries(fuel_type_id)               WHERE deleted_at IS NULL;
    -- Um cupom gera no máximo um abastecimento.
    CREATE UNIQUE INDEX UX_ABASTA_FE_receipt ON dbo.ABASTA_FuelEntries(receipt_id) WHERE receipt_id IS NOT NULL;
END
GO

------------------------------------------------------------
-- ABASTA_Budgets  (orçamento por empresa / usuário / veículo)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Budgets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Budgets
    (
        id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Budgets_id DEFAULT NEWSEQUENTIALID(),
        company_id       UNIQUEIDENTIFIER NOT NULL,
        user_id          UNIQUEIDENTIFIER NULL,
        vehicle_id       UNIQUEIDENTIFIER NULL,
        reference_month  DATE             NOT NULL,
        amount_limit     DECIMAL(18,2)    NOT NULL,
        alert_threshold  TINYINT          NOT NULL CONSTRAINT DF_ABASTA_Budgets_alert    DEFAULT (80),
        rollover         BIT              NOT NULL CONSTRAINT DF_ABASTA_Budgets_rollover DEFAULT (0),
        created_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Budgets_created  DEFAULT (SYSUTCDATETIME()),
        updated_at       DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Budgets_updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Budgets PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Budgets_company FOREIGN KEY (company_id) REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_Budgets_user    FOREIGN KEY (user_id)    REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT FK_ABASTA_Budgets_vehicle FOREIGN KEY (vehicle_id) REFERENCES dbo.ABASTA_Vehicles(id),
        CONSTRAINT CK_ABASTA_Budgets_limit  CHECK (amount_limit > 0),
        CONSTRAINT CK_ABASTA_Budgets_alert  CHECK (alert_threshold BETWEEN 0 AND 100),
        -- escopo: no máximo um alvo (usuário OU veículo); ambos nulos = orçamento da empresa.
        CONSTRAINT CK_ABASTA_Budgets_scope  CHECK (user_id IS NULL OR vehicle_id IS NULL)
    );

    CREATE UNIQUE INDEX UX_ABASTA_Budgets_company  ON dbo.ABASTA_Budgets(company_id, reference_month)             WHERE user_id IS NULL AND vehicle_id IS NULL;
    CREATE UNIQUE INDEX UX_ABASTA_Budgets_user     ON dbo.ABASTA_Budgets(company_id, user_id, reference_month)    WHERE user_id IS NOT NULL;
    CREATE UNIQUE INDEX UX_ABASTA_Budgets_vehicle  ON dbo.ABASTA_Budgets(company_id, vehicle_id, reference_month) WHERE vehicle_id IS NOT NULL;
END
GO

------------------------------------------------------------
-- ABASTA_Alerts  (alertas do painel do gestor)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Alerts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Alerts
    (
        id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Alerts_id DEFAULT NEWSEQUENTIALID(),
        company_id         UNIQUEIDENTIFIER NOT NULL,
        severity           VARCHAR(10)      NOT NULL CONSTRAINT DF_ABASTA_Alerts_sev DEFAULT ('info'),
        kind               VARCHAR(40)      NOT NULL,
        title              NVARCHAR(150)    NOT NULL,
        subtitle           NVARCHAR(300)    NULL,
        target_user_id     UNIQUEIDENTIFIER NULL,
        target_vehicle_id  UNIQUEIDENTIFIER NULL,
        reference_month    DATE             NULL,
        is_resolved        BIT              NOT NULL CONSTRAINT DF_ABASTA_Alerts_resolved DEFAULT (0),
        resolved_at        DATETIME2(3)     NULL,
        created_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Alerts_created DEFAULT (SYSUTCDATETIME()),
        updated_at         DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Alerts_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Alerts PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Alerts_company FOREIGN KEY (company_id)        REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_Alerts_user    FOREIGN KEY (target_user_id)    REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT FK_ABASTA_Alerts_vehicle FOREIGN KEY (target_vehicle_id) REFERENCES dbo.ABASTA_Vehicles(id),
        CONSTRAINT CK_ABASTA_Alerts_sev CHECK (severity IN ('danger','warn','success','info'))
    );

    CREATE INDEX IX_ABASTA_Alerts_company ON dbo.ABASTA_Alerts(company_id, is_resolved, created_at DESC);
END
GO

------------------------------------------------------------
-- ABASTA_Notifications  (notificações in-app / push por usuário)
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_Notifications
    (
        id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_Notif_id DEFAULT NEWSEQUENTIALID(),
        user_id        UNIQUEIDENTIFIER NOT NULL,
        kind           VARCHAR(30)      NOT NULL CONSTRAINT DF_ABASTA_Notif_kind DEFAULT ('system'),
        title          NVARCHAR(150)    NOT NULL,
        body           NVARCHAR(1000)   NULL,
        related_kind   NVARCHAR(40)     NULL,
        related_id     NVARCHAR(64)     NULL,
        scheduled_for  DATETIME2(3)     NULL,
        is_read        BIT              NOT NULL CONSTRAINT DF_ABASTA_Notif_read DEFAULT (0),
        read_at        DATETIME2(3)     NULL,
        created_at     DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Notif_created DEFAULT (SYSUTCDATETIME()),
        updated_at     DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_Notif_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_Notifications PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_Notif_user FOREIGN KEY (user_id) REFERENCES dbo.ABASTA_Users(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_ABASTA_Notif_user_read ON dbo.ABASTA_Notifications(user_id, is_read, created_at DESC);
END
GO

------------------------------------------------------------
-- ABASTA_ReportExports  (geração assíncrona de relatórios — "Baixar relatório")
------------------------------------------------------------
IF OBJECT_ID(N'dbo.ABASTA_ReportExports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ABASTA_ReportExports
    (
        id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ABASTA_RE_id DEFAULT NEWSEQUENTIALID(),
        company_id           UNIQUEIDENTIFIER NOT NULL,
        requested_by_user_id UNIQUEIDENTIFIER NOT NULL,
        scope                VARCHAR(15)      NOT NULL CONSTRAINT DF_ABASTA_RE_scope  DEFAULT ('company'),
        target_user_id       UNIQUEIDENTIFIER NULL,
        target_vehicle_id    UNIQUEIDENTIFIER NULL,
        period_start         DATE             NOT NULL,
        period_end           DATE             NOT NULL,
        format               VARCHAR(10)      NOT NULL CONSTRAINT DF_ABASTA_RE_format DEFAULT ('pdf'),
        status               VARCHAR(15)      NOT NULL CONSTRAINT DF_ABASTA_RE_status DEFAULT ('queued'),
        file_url             NVARCHAR(500)    NULL,
        error_message        NVARCHAR(500)    NULL,
        created_at           DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_RE_created DEFAULT (SYSUTCDATETIME()),
        updated_at           DATETIME2(3)     NOT NULL CONSTRAINT DF_ABASTA_RE_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ABASTA_ReportExports PRIMARY KEY (id),
        CONSTRAINT FK_ABASTA_RE_company FOREIGN KEY (company_id)           REFERENCES dbo.ABASTA_Companies(id),
        CONSTRAINT FK_ABASTA_RE_user    FOREIGN KEY (requested_by_user_id) REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT FK_ABASTA_RE_tuser   FOREIGN KEY (target_user_id)       REFERENCES dbo.ABASTA_Users(id),
        CONSTRAINT FK_ABASTA_RE_tveh    FOREIGN KEY (target_vehicle_id)    REFERENCES dbo.ABASTA_Vehicles(id),
        CONSTRAINT CK_ABASTA_RE_scope  CHECK (scope  IN ('company','user','vehicle')),
        CONSTRAINT CK_ABASTA_RE_format CHECK (format IN ('pdf','csv','xlsx')),
        CONSTRAINT CK_ABASTA_RE_status CHECK (status IN ('queued','processing','ready','failed')),
        CONSTRAINT CK_ABASTA_RE_period CHECK (period_end >= period_start)
    );

    CREATE INDEX IX_ABASTA_RE_company ON dbo.ABASTA_ReportExports(company_id, created_at DESC);
END
GO

PRINT 'Schema ABASTA_* criado/atualizado com sucesso.';
GO
