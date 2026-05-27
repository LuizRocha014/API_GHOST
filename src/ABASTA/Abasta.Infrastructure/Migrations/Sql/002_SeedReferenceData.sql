/*
    Abasta — migration 002: dados de referência (tipos de combustível).
    Os códigos/cores batem com o enum FuelType do app Flutter.
    Idempotente: só insere o que ainda não existe.
*/

MERGE dbo.ABASTA_FuelTypes AS target
USING (VALUES
    ('GASOLINA',   N'Gasolina',    '#F97316', 1),
    ('ETANOL',     N'Etanol',      '#FACC15', 2),
    ('DIESEL_S10', N'Diesel S-10', '#0F172A', 3),
    ('GNV',        N'GNV',         '#0284C7', 4)
) AS source (code, label, color_hex, sort_order)
ON target.code = source.code
WHEN MATCHED THEN
    UPDATE SET label = source.label, color_hex = source.color_hex,
               sort_order = source.sort_order, updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (code, label, color_hex, sort_order)
    VALUES (source.code, source.label, source.color_hex, source.sort_order);
GO

PRINT 'Tipos de combustível ABASTA semeados.';
GO
