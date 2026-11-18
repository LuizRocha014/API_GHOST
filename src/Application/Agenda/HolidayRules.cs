namespace Application.Agenda;

/// <summary>
/// Feriados que fecham a estética (mesma regra do app). Nacionais: os fixos da Lei 662/1949 e posteriores, o Dia da
/// Consciência Negra (Lei 14.759/2023) e a Sexta-feira Santa. Opcionais (pontos facultativos): segunda e terça de
/// Carnaval e Corpus Christi. Mais as datas que o dono cadastrar.
/// </summary>
public static class HolidayRules
{
    private static readonly (int Month, int Day, string Name)[] Fixed =
    [
        (1, 1, "Confraternização Universal"),
        (4, 21, "Tiradentes"),
        (5, 1, "Dia do Trabalho"),
        (9, 7, "Independência do Brasil"),
        (10, 12, "Nossa Senhora Aparecida"),
        (11, 2, "Finados"),
        (11, 15, "Proclamação da República"),
        (11, 20, "Dia da Consciência Negra"),
        (12, 25, "Natal"),
    ];

    /// <summary>Domingo de Páscoa (algoritmo de Meeus/Jones/Butcher, calendário gregoriano).</summary>
    public static DateTime Easter(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(year, month, day);
    }

    public static IReadOnlyList<(DateTime Date, string Name)> National(int year)
    {
        var list = Fixed.Select(x => (new DateTime(year, x.Month, x.Day), x.Name)).ToList();
        list.Add((Easter(year).AddDays(-2), "Sexta-feira Santa"));
        return list.OrderBy(x => x.Item1).ToList();
    }

    public static IReadOnlyList<(DateTime Date, string Name)> Optional(int year)
    {
        var easter = Easter(year);
        return
        [
            (easter.AddDays(-48), "Carnaval (segunda)"),
            (easter.AddDays(-47), "Carnaval (terça)"),
            (easter.AddDays(60), "Corpus Christi"),
        ];
    }

    /// <summary>Nome do feriado que fecha a estética no dia, ou nulo se abre normalmente.</summary>
    public static string? ClosedReason(AgendaConfigModel cfg, DateTime date)
    {
        var day = date.Date;
        var iso = AgendaFormat.Date(day);
        var custom = cfg.Holidays.Dates.FirstOrDefault(h => h.Date == iso);
        if (custom is not null) return string.IsNullOrWhiteSpace(custom.Name) ? "Fechado" : custom.Name;
        if (cfg.Holidays.National)
        {
            foreach (var (d, name) in National(day.Year))
                if (d == day) return name;
        }
        if (cfg.Holidays.Optional)
        {
            foreach (var (d, name) in Optional(day.Year))
                if (d == day) return name;
        }
        return null;
    }
}
