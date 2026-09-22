namespace MadVoxel.Core
{
    /// <summary>
    /// Coarse material families. Until real art exists every surface is a procedural
    /// grime texture tinted per block/item; the family decides the grain and gloss.
    /// </summary>
    public enum SurfaceFamily
    {
        Dirt,
        Grass,
        Stone,
        Sand,
        Wood,
        Plank,
        Metal,
        Concrete,
        Cloth,
        Foliage,
        Ore,
        Flesh,
        Emissive
    }
}
