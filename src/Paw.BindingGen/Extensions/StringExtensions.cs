namespace Paw.BindingGen.Extensions;

public static class StringExtensions
{
    public static string FirstToLower(this string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("String is empty or whitespace");

        return $"{char.ToLowerInvariant(s[0])}{s[1..]}";
    }

    public static string FirstToUpper(this string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("String is empty or whitespace");

        return $"{char.ToUpperInvariant(s[0])}{s[1..]}";
    }
}