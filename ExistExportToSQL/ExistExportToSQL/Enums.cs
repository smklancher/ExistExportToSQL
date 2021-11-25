namespace ExistExportToSQL;

public static class Enums
{
    public enum ComplexType
    {
        Averages,
        Correlations,
    }

    public static bool IsComplexType(string name) => Enum.TryParse<ComplexType>(name, true, out _);
}