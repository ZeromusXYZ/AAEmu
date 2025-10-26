using Jitter2.LinearMath;

namespace AAEmu.Game.Physics.HeightMaps;

public class Heightmap(ref float[,] heights, ref byte[,] materials)
{
    public float[,] Heights { get; init; } = heights;
    public byte[,] Materials { get; init; } = materials;
    public int Width => Heights.GetLength(0);
    public int Height => Heights.GetLength(1);
    public float MinHeight { get; } = heights.Cast<float>().Min();
    public float MaxHeight { get; } = heights.Cast<float>().Max();

    public float GetHeight(int x, int z) => Heights[x / 2, z / 2];
    public byte GetMaterial(int x, int z) => Materials[x / 2, z / 2];

    /// <summary>
    /// Real world bounding box 
    /// </summary>
    /// <returns></returns>
    public JBoundingBox GetBoundingBox()
    {
        var min = new JVector(0, MinHeight, 0);
        var max = new JVector((Width * 2) - 1, MaxHeight, (Height * 2) - 1);
        return new JBoundingBox(min, max);
    }
}
