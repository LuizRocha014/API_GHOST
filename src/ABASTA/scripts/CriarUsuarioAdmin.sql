/* ============================================================================
   ABASTA — cria um usuário GESTOR (admin)
   ----------------------------------------------------------------------------
   Rode no AbastaDB depois das migrations (tabelas já criadas).

   Usuário criado:
     Nome / display:  admin
     E-mail (login):  admin@abasta.com.br
     Senha:           admin123        (hash BCrypt — validado pela API)
     Papel:           admin (gestor)

   Como todo usuário pertence a uma empresa (FK obrigatória), o script garante
   uma empresa e cria o admin nela:
     - usa a empresa demo (11111111-…) se existir;
     - senão, usa a primeira empresa cadastrada;
     - senão, cria uma empresa "Abasta".

   Idempotente: se já existir um usuário com esse e-mail, não faz nada.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @AdminId   UNIQUEIDENTIFIER = '2ADADADA-0000-0000-0000-000000000001';
DECLARE @Email     NVARCHAR(255)    = 'admin@abasta.com.br';
DECLARE @Pwd       NVARCHAR(255)    = '$2a$11$5m.mq.CUn.0HyyUQXNi1UeArarZve.lHObMS3T64mz7NLb2qvHr2S'; -- admin123
DECLARE @DemoCompany UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

-- 1) Resolve a empresa do admin
DECLARE @CompanyId UNIQUEIDENTIFIER;

IF EXISTS (SELECT 1 FROM dbo.ABASTA_Companies WHERE id = @DemoCompany)
    SET @CompanyId = @DemoCompany;
ELSE
    SELECT TOP 1 @CompanyId = id FROM dbo.ABASTA_Companies ORDER BY created_at;

IF @CompanyId IS NULL
BEGIN
    SET @CompanyId = '1AAAAAAA-0000-0000-0000-000000000001';
    INSERT INTO dbo.ABASTA_Companies (id, name, legal_name)
    VALUES (@CompanyId, N'Abasta', N'Abasta LTDA');
END

-- 2) Cria o usuário gestor (se ainda não existir pelo e-mail)
IF NOT EXISTS (SELECT 1 FROM dbo.ABASTA_Users WHERE email = @Email)
BEGIN
    INSERT INTO dbo.ABASTA_Users
        (id, company_id, email, password_hash, full_name, display_name, role, job_title, is_active, email_verified)
    VALUES
        (@AdminId, @CompanyId, @Email, @Pwd, N'admin', N'admin', 'admin', N'Gestor', 1, 1);

    INSERT INTO dbo.ABASTA_NotificationPreferences (user_id)
    SELECT @AdminId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ABASTA_NotificationPreferences WHERE user_id = @AdminId);

    PRINT N'OK - usuário gestor criado.';
END
ELSE
BEGIN
    PRINT N'Usuário admin@abasta.com.br já existe — nada a fazer.';
END

COMMIT TRAN;

PRINT N'  Login: admin@abasta.com.br';
PRINT N'  Senha: admin123';
GO
