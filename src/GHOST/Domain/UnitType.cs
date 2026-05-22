namespace Domain;

public static class UnitTypes
{
    public const string Un = "UN";
    public const string Kg = "KG";
    public const string Lt = "LT";

    public static bool IsValid(string value) =>
        value is Un or Kg or Lt;
}
