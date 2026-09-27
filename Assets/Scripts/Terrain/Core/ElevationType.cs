namespace TerrainGeneration
{
    /// <summary>
    /// Represents what type of terrain this is.
    /// Exists to later map textures to terrain elevation.
    /// </summary>
    public enum ElevationType
    {
        DeepOcean, // Deep ocean floor
        ShallowWater, // Shallow underwater terrain
        Shore, // Beaches, coastlines
        Lowland, // Plains, low-elevation terrain
        Highland, // Hills, elevated terrain
        MountainBase, // Lower mountain slopes
        Mountain, // Main mountain slopes
        MountainPeak // Highest mountain elevations
    }
}