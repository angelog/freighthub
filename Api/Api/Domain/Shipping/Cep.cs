using System.Text.RegularExpressions;

namespace Api.Domain.Shipping;

/// <summary>
/// Utilitários para normalização e comparação de CEP.
/// Um CEP é sempre tratado como 8 dígitos (sem hífen); por ser de tamanho fixo,
/// a comparação lexicográfica equivale à comparação numérica.
/// </summary>
public static partial class Cep
{
    /// <summary>
    /// Remove qualquer caractere não numérico e valida que o resultado tem 8 dígitos.
    /// </summary>
    public static string Normalize(string cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
            throw new ArgumentException("CEP não pode ser vazio.", nameof(cep));

        var digits = NonDigits().Replace(cep, string.Empty);

        if (digits.Length != 8)
            throw new ArgumentException($"CEP inválido: '{cep}'. Esperado 8 dígitos.", nameof(cep));

        return digits;
    }

    /// <summary>
    /// Tenta normalizar o CEP; retorna false em vez de lançar exceção.
    /// </summary>
    public static bool TryNormalize(string? cep, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(cep))
            return false;

        var digits = NonDigits().Replace(cep, string.Empty);
        if (digits.Length != 8)
            return false;

        normalized = digits;
        return true;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}
