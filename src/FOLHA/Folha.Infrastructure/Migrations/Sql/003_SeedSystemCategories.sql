/*
    Folha — migration 003: seed das categorias do sistema (user_id NULL, is_system=1).
    IDs determinísticos (1-8) para o app fazer lookup por slug.
*/

SET IDENTITY_INSERT dbo.FOLHA_Categories ON;

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'food' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (1, NULL, 'food', N'Alimentação', N'utensils', '#6F2C1C', '#F0D9CE', 'expense', 1, 10);

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'trans' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (2, NULL, 'trans', N'Transporte', N'car', '#1A3A2E', '#D2E4D8', 'expense', 1, 20);

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'leisure' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (3, NULL, 'leisure', N'Lazer', N'sparkles', '#8C6E2A', '#F2E8CC', 'expense', 1, 30);

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'home' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (4, NULL, 'home', N'Casa', N'home', '#1A1612', '#D5CDB8', 'expense', 1, 40);

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'health' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (5, NULL, 'health', N'Saúde', N'heart-pulse', '#0B1F18', '#D2E4D8', 'expense', 1, 50);

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'shop' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (6, NULL, 'shop', N'Compras', N'shopping-bag', '#8C6E2A', '#F2E8CC', 'expense', 1, 60);

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'income' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (7, NULL, 'income', N'Entrada', N'arrow-down-circle', '#0B1F18', '#D2E4D8', 'income', 1, 70);

IF NOT EXISTS (SELECT 1 FROM dbo.FOLHA_Categories WHERE slug = 'other' AND user_id IS NULL)
    INSERT INTO dbo.FOLHA_Categories (id, user_id, slug, label, icon, color_hex, bg_hex, kind, is_system, sort_order)
    VALUES (8, NULL, 'other', N'Outros', N'more-horizontal', '#1A1612', '#D5CDB8', 'expense', 1, 80);

SET IDENTITY_INSERT dbo.FOLHA_Categories OFF;
GO
