using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Application.Agenda;

namespace Application.Bot;

/// <summary>Formatação dos textos do bot (mesma do app: "Sex 07/11", "1h30", "R$ 1.200", "R$ 49,90").</summary>
public static partial class BotFormat
{
    private static readonly string[] WeekdaysShort = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];
    private static readonly string[] WeekdaysLong = ["Domingo", "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado"];
    private static readonly string[] Months =
        ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    public static DateTime ParseDate(string iso) => DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"Sex 07/11".</summary>
    public static string DayLabel(string iso)
    {
        var d = ParseDate(iso);
        return $"{WeekdaysShort[(int)d.DayOfWeek]} {d:dd}/{d:MM}";
    }

    /// <summary>"Sexta-feira, 7 de novembro".</summary>
    public static string DayLong(string iso)
    {
        var d = ParseDate(iso);
        return $"{WeekdaysLong[(int)d.DayOfWeek]}, {d.Day} de {Months[d.Month - 1]}";
    }

    public static string Hm(int minutes) => AgendaFormat.Hm(minutes);

    /// <summary>"novembro".</summary>
    public static string MonthName(string iso) => Months[ParseDate(iso).Month - 1];

    /// <summary>
    /// Horário de funcionamento agrupando dias seguidos iguais, de segunda a domingo:
    /// "Seg a Sex: 08:00 às 18:00", "Sáb: 08:00 às 13:00", "Dom: fechado".
    /// </summary>
    public static IReadOnlyList<string> OpeningHours(IReadOnlyList<int[]?> hours)
    {
        int[] order = [1, 2, 3, 4, 5, 6, 0];
        string Value(int day)
        {
            var h = day < hours.Count ? hours[day] : null;
            return h is { Length: >= 2 } && h[1] > h[0] ? $"{Hm(h[0])} às {Hm(h[1])}" : "fechado";
        }

        var lines = new List<string>();
        var i = 0;
        while (i < order.Length)
        {
            var j = i;
            while (j + 1 < order.Length && Value(order[j + 1]) == Value(order[i])) j++;
            var label = i == j ? WeekdaysShort[order[i]] : $"{WeekdaysShort[order[i]]} a {WeekdaysShort[order[j]]}";
            lines.Add($"{label}: {Value(order[i])}");
            i = j + 1;
        }
        return lines;
    }

    /// <summary>"45 min", "1h", "1h30".</summary>
    public static string Duration(int minutes)
    {
        var h = minutes / 60;
        var r = minutes % 60;
        return h == 0 ? $"{r} min" : $"{h}h{(r > 0 ? r.ToString("00", CultureInfo.InvariantCulture) : string.Empty)}";
    }

    /// <summary>"R$ 50", "R$ 1.200", "R$ 49,90".</summary>
    public static string Money(decimal? value)
    {
        if (value is null) return string.Empty;
        var cents = (long)Math.Round(value.Value * 100m, MidpointRounding.AwayFromZero);
        var whole = (cents / 100).ToString(CultureInfo.InvariantCulture);
        var frac = cents % 100;
        var sb = new StringBuilder();
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0) sb.Append('.');
            sb.Append(whole[i]);
        }
        return $"R$ {sb}{(frac == 0 ? string.Empty : "," + frac.ToString("00", CultureInfo.InvariantCulture))}";
    }

    public static string FirstName(string? name)
    {
        var t = (name ?? string.Empty).Trim();
        return t.Length == 0 ? string.Empty : WhitespaceRegex().Split(t)[0];
    }

    public static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    public static string StripAccents(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Texto para comparar comandos: sem acento, minúsculo, sem pontuação, espaços únicos.</summary>
    public static string Normalize(string? t)
    {
        if (t is null) return string.Empty;
        var s = PunctuationRegex().Replace(StripAccents(t).ToLowerInvariant(), " ");
        return WhitespaceRegex().Replace(s, " ").Trim();
    }

    /// <summary>Troca {variavel} pelos valores; variável desconhecida fica como está.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        VariableRegex().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    /// <summary>Variáveis do texto, na ordem da primeira aparição, sem repetir.</summary>
    public static IReadOnlyList<string> Variables(string text)
    {
        var list = new List<string>();
        foreach (Match m in VariableRegex().Matches(text))
            if (!list.Contains(m.Groups[1].Value)) list.Add(m.Groups[1].Value);
        return list;
    }

    /// <summary>"Onix prata ABC-1D23" → (Onix prata, ABC1D23).</summary>
    public static (string Vehicle, string Plate) ExtractVehicle(string text)
    {
        var m = PlateRegex().Match(text);
        var plate = m.Success ? (m.Groups[1].Value + m.Groups[2].Value).ToUpperInvariant() : string.Empty;
        var rest = m.Success ? text.Remove(m.Index, m.Length).Insert(m.Index, " ") : text;
        rest = VehicleWordsRegex().Replace(rest, " ");
        rest = SeparatorsRegex().Replace(rest, " ");
        rest = WhitespaceRegex().Replace(rest, " ").Trim();
        return (rest, plate);
    }

    /// <summary>Valores das variáveis dos textos para um agendamento.</summary>
    public static Dictionary<string, string> BookingValues(BookingDto b, string businessName, string boxName)
    {
        var name = FirstName(b.ClientName);
        return new Dictionary<string, string>
        {
            ["nome"] = name.Length == 0 ? "cliente" : name,
            ["estetica"] = businessName,
            ["servico"] = b.ServiceName,
            ["data"] = DayLabel(b.Date),
            ["hora"] = Hm(b.Start),
            ["fim"] = Hm(b.End),
            ["box"] = boxName,
            ["veiculo"] = string.Join(" · ", new[] { b.Vehicle, b.Plate }.Where(x => !string.IsNullOrEmpty(x))),
            ["preco"] = b.Price is null ? string.Empty : Money(b.Price),
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[!?.,;*_~]+")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex VariableRegex();

    [GeneratedRegex(@"\b([A-Za-z]{3})[-\s]?(\d[A-Za-z0-9]\d{2})\b")]
    private static partial Regex PlateRegex();

    [GeneratedRegex(@"\b(placa|modelo|carro|veiculo|veículo)\b:?", RegexOptions.IgnoreCase)]
    private static partial Regex VehicleWordsRegex();

    [GeneratedRegex(@"[,;|]+")]
    private static partial Regex SeparatorsRegex();
}
