using System.Security.Cryptography;

namespace Application.Auth;

/// <summary>Regra mínima de senha e gerador de senha provisória.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Senha do seed (pública no repositório): nunca vale como senha definitiva.</summary>
    public const string SeedPassword = "Admin@123";

    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        SeedPassword, "12345678", "123456789", "1234567890", "senha123", "senha1234", "password", "password1", "qwerty123",
        "abc12345", "admin123", "admin1234", "estetica1", "estetica123", "11111111", "00000000",
    };

    /// <summary>Mensagem do problema, ou nulo se a senha serve.</summary>
    public static string? Problem(string? password, string username)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"A senha precisa ter pelo menos {MinLength} caracteres.";
        if (password.Length > 128) return "A senha pode ter no máximo 128 caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Use letras e números na senha.";
        if (Common.Contains(password) || password.Equals(username, StringComparison.OrdinalIgnoreCase))
            return "Essa senha é fácil de adivinhar. Escolha outra.";
        return null;
    }

    /// <summary>Senha provisória legível (sem 0/O, 1/l/I): 4 letras, 4 números e 2 letras.</summary>
    public static string Temporary()
    {
        const string letters = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        Span<char> s = stackalloc char[10];
        for (var i = 0; i < s.Length; i++)
        {
            var pool = i is >= 4 and < 8 ? digits : letters;
            s[i] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }
        return new string(s);
    }
}
