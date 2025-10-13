namespace AAEmu.Game.Models.CryEngine.Objects;

public class PrefabDataType6Voxel : PrefabData
{
    public int NumRanges { get; set; }
    public List<byte[]> ChunkData { get; set; } = [];
}
