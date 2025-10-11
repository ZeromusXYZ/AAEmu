namespace AAEmu.Game.Models.ClientData;

public class Hmap()
{
    public byte Version { get; set; }
    public byte Dummy { get; set; }
    public byte Flags { get; set; }
    public byte Flags2 { get; set; }
    public int ChunkSize { get; set; }
    public int HeightMapSizeInUnits { get; set; }
    public int UnitSizeInMeters { get; set; }
    public int SectorSizeInMeters { get; set; }
    public int SectorsTableSizeInSectors { get; set; }
    public float HeightmapZRatio { get; set; }
    public float OceanWaterLevel { get; set; }

    private List<NodeCell> Nodes { get; set; } = [];
    public List<NodeCell> SortedNodes { get; private set; } = [];

    public int Read(BinaryReader br)
    {
        Version = br.ReadByte();
        Dummy = br.ReadByte();
        Flags = br.ReadByte();
        Flags2 = br.ReadByte();

        // TODO: spawn endian, flags & 1 ? eBigEndian : eLittleEndian

        ChunkSize = br.ReadInt32();
        HeightMapSizeInUnits = br.ReadInt32();
        UnitSizeInMeters = br.ReadInt32();
        SectorSizeInMeters = br.ReadInt32();
        SectorsTableSizeInSectors = br.ReadInt32();
        HeightmapZRatio = br.ReadSingle();
        OceanWaterLevel = br.ReadSingle();

        if (Version >= 24)
            br.ReadBytes(128); // unk?

        var nodesRead = 0;
        while (br.BaseStream.Position != ChunkSize)
        {
            var node = new NodeCell();
            try
            {
                node.Read(br);
                nodesRead++;
            }
            catch
            {
                return -1;
            }

            Nodes.Add(node);
        }
        return nodesRead;
    }

    /// <summary>
    /// Sorts nodes by expected position 
    /// </summary>
    public void SortNodes()
    {
        // Sort nodes by position
        SortedNodes = Nodes
            .OrderBy(cell => cell.BoxHeightmap.Min.X)
            .ThenBy(cell => cell.BoxHeightmap.Min.Y)
            .Where(x => x.HasData())
            .ToList();
    }
}
