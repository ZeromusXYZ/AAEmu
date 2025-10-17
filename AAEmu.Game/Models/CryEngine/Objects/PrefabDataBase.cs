using System.Numerics;

namespace AAEmu.Game.Models.CryEngine.Objects;

public class PrefabDataBase(int prefabType)
{
    public static Dictionary<int, int> ObjectTotalSizesByType { get; } = new()
    {
        {1, 132},  // Brushes
        {2, 68},   // Vegetation
        {4, 100},  // 
        {5, 124},  //
        {8, 196},  //
        {9, 115},  // Decal
        {14, 83},  // Distance Clouds
        {27, 652}, // 
    };

    public int PrefabType { get; init; } = prefabType;
    public virtual bool IsGeneric { get; protected init; } = true;
    public byte[] Data { get; protected set; } = [];

    /// <summary>
    /// Read the data from a byte array starting at offset
    /// </summary>
    /// <param name="blockData"></param>
    /// <param name="offset"></param>
    /// <returns>Number of bytes used</returns>
    public virtual int ReadData(byte[] blockData, int offset)
    {
        var bytesToRead = ObjectTotalSizesByType.GetValueOrDefault(PrefabType);
        if (bytesToRead + offset > blockData.Length)
        {
            Data = [];
            return 0;
        }
        Data = blockData.Skip(offset).Take(bytesToRead).ToArray();
        return Data.Length;
    }
    
    protected Vector3 GetVector3(byte[] blockData, int offset)
    {
        return new Vector3(
            BitConverter.ToSingle(blockData, offset + 0),
            BitConverter.ToSingle(blockData, offset + 4),
            BitConverter.ToSingle(blockData, offset + 8)
        );
    }
}
