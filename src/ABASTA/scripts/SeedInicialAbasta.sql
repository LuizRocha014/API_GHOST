/* ============================================================================
   ABASTA — SEED INICIAL
   ----------------------------------------------------------------------------
   Rode este script DIRETO no banco (AbastaDB), DEPOIS que as migrations
   001/002/003 já tiverem criado as tabelas e semeado os tipos de combustível.

   Cria tudo que é preciso para já começar a usar o app:
     • 1 empresa (tenant)         • 1 conta na distribuidora
     • 1 gestor + 6 colaboradores • preferências de notificação de cada um
     • 6 veículos + vínculos      • 6 postos
     • orçamentos (empresa + por colaborador)
     • abastecimentos de exemplo (para o dashboard/relatórios não nascerem vazios)

   LOGIN (todos):  senha = abasta123
     Gestor:        renata.lopes@empresa.com.br
     Colaborador:   joao.silva@empresa.com.br   (e os demais @empresa.com.br)

   O script é IDEMPOTENTE: pode rodar várias vezes sem duplicar nada.
   Para zerar e recriar, veja o bloco de LIMPEZA comentado no fim.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

------------------------------------------------------------------------------
-- IDs fixos (facilita re-rodar e referenciar)
------------------------------------------------------------------------------
DECLARE @CompanyId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @FuelAccId UNIQUEIDENTIFIER = '1ACC0000-0000-0000-0000-000000000001';

DECLARE @Renata UNIQUEIDENTIFIER = '20000000-0000-0000-0000-0000000000A1';
DECLARE @Joao   UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000001';
DECLARE @Maria  UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000002';
DECLARE @Pedro  UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000003';
DECLARE @Ana    UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000004';
DECLARE @Carlos UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000005';
DECLARE @Fer    UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000006';

DECLARE @V1 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000001'; -- João
DECLARE @V2 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000002'; -- Maria
DECLARE @V3 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000003'; -- Pedro
DECLARE @V4 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000004'; -- Ana
DECLARE @V5 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000005'; -- Carlos
DECLARE @V6 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000006'; -- Fernanda

DECLARE @S1 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000001'; -- Shell Centro
DECLARE @S2 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000002'; -- Ipiranga BR-101
DECLARE @S3 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000003'; -- Petrobras Rod. Norte
DECLARE @S4 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000004'; -- BR Mania Rod. Sul
DECLARE @S5 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000005'; -- Ipiranga Av. Central
DECLARE @S6 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000006'; -- Petrobras Av. Brasil

-- Senha "abasta123" (hash BCrypt — a API valida com BCrypt.Verify)
DECLARE @Pwd NVARCHAR(255) = '$2a$11$9jOW6juDaLCFhOCziO/VvOSy38ni0IIH03ISh2b4dS5vPUqHOAn0S';

-- Mês de referência dos orçamentos = 1º dia do mês corrente
DECLARE @Month DATE = DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1);

-- Tipos de combustível (semeados pela migration 002) — busca por código
DECLARE @Gas INT, @Eta INT, @Die INT, @Gnv INT;
SELECT @Gas = id FROM dbo.ABASTA_FuelTypes WHERE code = 'GASOLINA';
SELECT @Eta = id FROM dbo.ABASTA_FuelTypes WHERE code = 'ETANOL';
SELECT @Die = id FROM dbo.ABASTA_FuelTypes WHERE code = 'DIESEL_S10';
SELECT @Gnv = id FROM dbo.ABASTA_FuelTypes WHERE code = 'GNV';

IF @Gas IS NULL
BEGIN
    RAISERROR('Tipos de combustível não encontrados. Rode a migration 002 antes deste seed.', 16, 1);
    ROLLBACK TRAN; RETURN;
END

------------------------------------------------------------------------------
-- 1) Empresa + conta na distribuidora
------------------------------------------------------------------------------
INSERT INTO dbo.ABASTA_Companies (id, name, legal_name, cnpj, monthly_budget)
SELECT @CompanyId, N'Frota Demo Abasta', N'Frota Demo Abasta LTDA', '12345678000190', 18000.00
WHERE NOT EXISTS (SELECT 1 FROM dbo.ABASTA_Companies WHERE id = @CompanyId);

INSERT INTO dbo.ABASTA_FuelAccounts (id, company_id, distributor, account_number)
SELECT @FuelAccId, @CompanyId, N'BR Distribuidora', N'BR-2026-0001'
WHERE NOT EXISTS (SELECT 1 FROM dbo.ABASTA_FuelAccounts WHERE id = @FuelAccId);

------------------------------------------------------------------------------
-- 2) Usuários (1 gestor + 6 colaboradores). Todos já verificados e ativos.
------------------------------------------------------------------------------
INSERT INTO dbo.ABASTA_Users
    (id, company_id, email, password_hash, full_name, display_name, role, job_title, is_active, email_verified)
SELECT v.id, @CompanyId, v.email, @Pwd, v.full_name, v.display_name, v.role, v.job_title, 1, 1
FROM (VALUES
    (@Renata, 'renata.lopes@empresa.com.br',  N'Renata Lopes',  N'Renata',  'admin',         N'Gestora de frota'),
    (@Joao,   'joao.silva@empresa.com.br',    N'João Silva',    N'João',    'collaborator',  N'Motorista'),
    (@Maria,  'maria.rocha@empresa.com.br',   N'Maria Rocha',   N'Maria',   'collaborator',  N'Vendas'),
    (@Pedro,  'pedro.castro@empresa.com.br',  N'Pedro Castro',  N'Pedro',   'collaborator',  N'Técnico de campo'),
    (@Ana,    'ana.beatriz@empresa.com.br',   N'Ana Beatriz',   N'Ana',     'collaborator',  N'Coordenadora'),
    (@Carlos, 'carlos.lima@empresa.com.br',   N'Carlos Lima',   N'Carlos',  'collaborator',  N'Motorista'),
    (@Fer,    'fernanda.dias@empresa.com.br', N'Fernanda Dias', N'Fernanda','collaborator',  N'Logística')
) AS v(id, email, full_name, display_name, role, job_title)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ABASTA_Users u WHERE u.id = v.id);

-- Preferências de notificação (defaults) para quem ainda não tem
INSERT INTO dbo.ABASTA_NotificationPreferences (user_id)
SELECT u.id FROM dbo.ABASTA_Users u
WHERE u.company_id = @CompanyId
  AND NOT EXISTS (SELECT 1 FROM dbo.ABASTA_NotificationPreferences p WHERE p.user_id = u.id);

------------------------------------------------------------------------------
-- 3) Veículos da frota
------------------------------------------------------------------------------
INSERT INTO dbo.ABASTA_Vehicles
    (id, company_id, fuel_account_id, default_fuel_type_id, plate, label, make, model, model_year, monthly_budget, is_active)
SELECT v.id, @CompanyId, @FuelAccId, v.fuel, v.plate, v.label, v.make, v.model, v.year, 3000.00, 1
FROM (VALUES
    (@V1, @Gas, 'ABC1D23', N'Onix do João',      N'Chevrolet', N'Onix',    2022),
    (@V2, @Die, 'DEF4G56', N'Hilux da Maria',     N'Toyota',    N'Hilux',   2021),
    (@V3, @Eta, 'GHI7J89', N'Strada do Pedro',    N'Fiat',      N'Strada',  2023),
    (@V4, @Gas, 'JKL1M02', N'HB20 da Ana',        N'Hyundai',   N'HB20',    2022),
    (@V5, @Die, 'MNO3P45', N'Sprinter do Carlos', N'Mercedes',  N'Sprinter',2020),
    (@V6, @Gas, 'PQR6S78', N'Kwid da Fernanda',   N'Renault',   N'Kwid',    2023)
) AS v(id, fuel, plate, label, make, model, year)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ABASTA_Vehicles x WHERE x.id = v.id);

-- Vínculo motorista ↔ veículo (primário, ativo)
INSERT INTO dbo.ABASTA_VehicleAssignments (id, company_id, vehicle_id, user_id, is_primary)
SELECT NEWID(), @CompanyId, v.vehicle_id, v.user_id, 1
FROM (VALUES
    (@V1, @Joao), (@V2, @Maria), (@V3, @Pedro), (@V4, @Ana), (@V5, @Carlos), (@V6, @Fer)
) AS v(vehicle_id, user_id)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ABASTA_VehicleAssignments a
    WHERE a.vehicle_id = v.vehicle_id AND a.unassigned_at IS NULL AND a.is_primary = 1);

------------------------------------------------------------------------------
-- 4) Postos
------------------------------------------------------------------------------
INSERT INTO dbo.ABASTA_Stations (id, company_id, name, brand, city, state, is_active)
SELECT v.id, @CompanyId, v.name, v.brand, v.city, v.state, 1
FROM (VALUES
    (@S1, N'Posto Shell Centro',     N'Shell',     N'São Paulo', 'SP'),
    (@S2, N'Ipiranga BR-101',        N'Ipiranga',  N'Curitiba',  'PR'),
    (@S3, N'Petrobras Rod. Norte',   N'Petrobras', N'Campinas',  'SP'),
    (@S4, N'BR Mania Rod. Sul',      N'BR',        N'Joinville', 'SC'),
    (@S5, N'Ipiranga Av. Central',   N'Ipiranga',  N'São Paulo', 'SP'),
    (@S6, N'Petrobras Av. Brasil',   N'Petrobras', N'Rio de Janeiro', 'RJ')
) AS v(id, name, brand, city, state)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ABASTA_Stations x WHERE x.id = v.id);

------------------------------------------------------------------------------
-- 5) Orçamentos: da empresa + de cada colaborador (R$ 3.000 no mês corrente)
------------------------------------------------------------------------------
-- Empresa
INSERT INTO dbo.ABASTA_Budgets (id, company_id, user_id, vehicle_id, reference_month, amount_limit, alert_threshold)
SELECT NEWID(), @CompanyId, NULL, NULL, @Month, 18000.00, 80
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ABASTA_Budgets b
    WHERE b.company_id = @CompanyId AND b.user_id IS NULL AND b.vehicle_id IS NULL AND b.reference_month = @Month);

-- Por colaborador
INSERT INTO dbo.ABASTA_Budgets (id, company_id, user_id, vehicle_id, reference_month, amount_limit, alert_threshold)
SELECT NEWID(), @CompanyId, u.id, NULL, @Month, 3000.00, 80
FROM dbo.ABASTA_Users u
WHERE u.company_id = @CompanyId AND u.role = 'collaborator'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.ABASTA_Budgets b
    WHERE b.company_id = @CompanyId AND b.user_id = u.id AND b.reference_month = @Month);

------------------------------------------------------------------------------
-- 6) Abastecimentos de exemplo (opcional — para dashboard/relatórios já terem dados)
--    Remova este bloco se quiser começar sem histórico.
------------------------------------------------------------------------------
INSERT INTO dbo.ABASTA_FuelEntries
    (id, company_id, user_id, vehicle_id, fuel_type_id, station_id, station_name,
     liters, price_per_liter, total_amount, odometer_km, fueled_at, status, source)
SELECT v.id, @CompanyId, v.user_id, v.vehicle_id, v.fuel, v.station_id, v.station_name,
       v.liters, v.ppl, v.total, v.km, DATEADD(DAY, v.days_ago, SYSUTCDATETIME()), 'registered', 'manual'
FROM (VALUES
    ('5E000000-0000-0000-0000-000000000001', @Joao,  @V1, @Gas, @S1, N'Posto Shell Centro',   38.500, 5.890, 226.75, 48230,  0),
    ('5E000000-0000-0000-0000-000000000002', @Maria, @V2, @Die, @S2, N'Ipiranga BR-101',       52.000, 7.930, 412.40, 92418, -1),
    ('5E000000-0000-0000-0000-000000000003', @Pedro, @V3, @Eta, @S3, N'Petrobras Rod. Norte',  22.000, 4.070,  89.55, 33102, -3),
    ('5E000000-0000-0000-0000-000000000004', @Joao,  @V1, @Gas, @S1, N'Posto Shell Centro',    41.200, 5.890, 242.66, 47830, -6),
    ('5E000000-0000-0000-0000-000000000005', @Carlos,@V5, @Die, @S4, N'BR Mania Rod. Sul',     60.000, 7.970, 478.20,121040, -7),
    ('5E000000-0000-0000-0000-000000000006', @Ana,   @V4, @Gas, @S5, N'Ipiranga Av. Central',  28.000, 5.890, 164.92, 18230, -8),
    ('5E000000-0000-0000-0000-000000000007', @Joao,  @V1, @Gas, @S6, N'Petrobras Av. Brasil',  35.000, 5.890, 206.15, 47420,-11),
    ('5E000000-0000-0000-0000-000000000008', @Maria, @V2, @Die, @S2, N'Ipiranga BR-101',       48.500, 7.930, 384.66, 91980,-12)
) AS v(id, user_id, vehicle_id, fuel, station_id, station_name, liters, ppl, total, km, days_ago)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ABASTA_FuelEntries x WHERE x.id = v.id);

------------------------------------------------------------------------------
-- 7) Convite de exemplo (acesso pendente que um gestor enviaria a um novo colaborador)
--    O hash do token é fictício; convites reais são criados via POST /api/invitations.
------------------------------------------------------------------------------
INSERT INTO dbo.ABASTA_Invitations
    (id, company_id, email, role, invited_by_user_id, token_hash, status, expires_at)
SELECT '17000000-0000-0000-0000-000000000001', @CompanyId, 'novo.motorista@empresa.com.br',
       'collaborator', @Renata, 'SEED_TOKEN_HASH_EXEMPLO_NAO_USAVEL', 'pending', DATEADD(DAY, 7, SYSUTCDATETIME())
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ABASTA_Invitations i WHERE i.id = '17000000-0000-0000-0000-000000000001');

COMMIT TRAN;

PRINT N'OK - Seed inicial Abasta aplicado.';
PRINT N'  Empresa: Frota Demo Abasta';
PRINT N'  Gestor:    renata.lopes@empresa.com.br  (senha: abasta123)';
PRINT N'  Motorista: joao.silva@empresa.com.br   (senha: abasta123)';
GO

/* ============================================================================
   LIMPEZA (opcional) — apaga só os dados deste seed, respeitando as FKs.
   Descomente e rode se quiser recriar do zero.
   ----------------------------------------------------------------------------
DECLARE @C UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DELETE FROM dbo.ABASTA_FuelEntries        WHERE company_id = @C;
DELETE FROM dbo.ABASTA_Budgets            WHERE company_id = @C;
DELETE FROM dbo.ABASTA_Invitations        WHERE company_id = @C;
DELETE FROM dbo.ABASTA_AccessLogs         WHERE company_id = @C;
DELETE FROM dbo.ABASTA_VehicleAssignments WHERE company_id = @C;
DELETE FROM dbo.ABASTA_Stations           WHERE company_id = @C;
DELETE FROM dbo.ABASTA_Vehicles           WHERE company_id = @C;
DELETE FROM dbo.ABASTA_NotificationPreferences
       WHERE user_id IN (SELECT id FROM dbo.ABASTA_Users WHERE company_id = @C);
DELETE FROM dbo.ABASTA_RefreshTokens
       WHERE user_id IN (SELECT id FROM dbo.ABASTA_Users WHERE company_id = @C);
DELETE FROM dbo.ABASTA_EmailVerifications
       WHERE user_id IN (SELECT id FROM dbo.ABASTA_Users WHERE company_id = @C);
DELETE FROM dbo.ABASTA_Sessions
       WHERE user_id IN (SELECT id FROM dbo.ABASTA_Users WHERE company_id = @C);
DELETE FROM dbo.ABASTA_Users              WHERE company_id = @C;
DELETE FROM dbo.ABASTA_FuelAccounts       WHERE company_id = @C;
DELETE FROM dbo.ABASTA_Companies          WHERE id = @C;
   ============================================================================ */
