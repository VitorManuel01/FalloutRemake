using FalloutRemake.Runtime;

namespace FalloutRemake.Preview;

public static class CarryWeightText
{
    public static string Load(long? weight, int capacity) => $"{weight?.ToString() ?? "indisponível"} / {capacity} lb";
    public static string Stack(long? unitWeight, int quantity) => quantity == 0 ? "0 lb"
        : CarryWeight.Total(new[] { (unitWeight, quantity) }) is long weight ? $"{weight} lb" : "indisponível";
}
