namespace Umpk.Game.Entities;

/// <summary>
/// The villager type and profession registry-id tables. The <see cref="VillagerData"/> network ids index vanilla's
/// built-in registries (<c>minecraft:villager_type</c>, <c>minecraft:villager_profession</c>), whose registration order
/// is fixed by the client's own code and never sent by the server, so the names are table constants here rather than
/// dataset lookups. The order has been stable since 1.14 introduced the metadata field.
/// </summary>
public static class VillagerDataNames
{
    private static readonly string?[] Types =
    [
        "minecraft:desert", "minecraft:jungle", "minecraft:plains", "minecraft:savanna",
        "minecraft:snow", "minecraft:swamp", "minecraft:taiga",
    ];

    private static readonly string?[] Professions =
    [
        "minecraft:none", "minecraft:armorer", "minecraft:butcher", "minecraft:cartographer",
        "minecraft:cleric", "minecraft:farmer", "minecraft:fisherman", "minecraft:fletcher",
        "minecraft:leatherworker", "minecraft:librarian", "minecraft:mason", "minecraft:nitwit",
        "minecraft:shepherd", "minecraft:toolsmith", "minecraft:weaponsmith",
    ];

    /// <summary>The namespaced villager-type id (the biome variant) for a network id, or null when the id is out of the known range.</summary>
    public static string? TypeName(int id) => Lookup(Types, id);

    /// <summary>The namespaced villager-profession id for a network id, or null when the id is out of the known range.</summary>
    public static string? ProfessionName(int id) => Lookup(Professions, id);

    private static string? Lookup(string?[] table, int id) => id >= 0 && id < table.Length ? table[id] : null;
}
