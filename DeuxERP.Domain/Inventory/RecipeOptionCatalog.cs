namespace DeuxERP.Domain.Inventory;

public static class RecipeOptionCatalog
{
    public static readonly string[] CakeDoughs =
    [
        "Baunilha",
        "Red Velvet",
        "Chocolate",
        "Limão",
        "Caramelo"
    ];

    public static readonly string[] CakeFillings =
    [
        "brulee",
        "branco",
        "doce de leite",
        "limao",
        "beijinho",
        "chocolate",
        "cream cheese frosting"
    ];

    public static readonly string[] BrigadeiroFlavors =
    [
        "chocolate",
        "brulee",
        "beijinho",
        "limão",
        "churros",
        "casadinho"
    ];

    public static readonly string[] CookieFlavors =
    [
        "churros",
        "cacau",
        "tradicional",
        "brookie",
        "caramelo salgado"
    ];

    public static readonly string[] AllFlavors = BrigadeiroFlavors
        .Concat(CookieFlavors)
        .Concat(CakeFillings)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
