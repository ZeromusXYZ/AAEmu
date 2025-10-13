using System.Diagnostics;
using System.Numerics;
using System.Text;
using AAEmu.Game.IO;
using NLog;

namespace AAEmu.Game.Models.CryEngine.Objects;

public class ObjectsFile(string fileName)
{
    private static Logger Logger = LogManager.GetCurrentClassLogger();
    private const uint NodeHeaderSize = 33;

    private static Dictionary<int, int> ObjectTotalSizesByType { get; } = new()
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

    public string FileName { get; init; } = fileName;
    public List<AssetPath> AssetPathsList { get; set; } = [];
    public List<PrefabData> PrefabsList { get; set; } = [];

    public bool ReadFile()
    {
        AssetPathsList.Clear();
        PrefabsList.Clear();
        try
        {
            using var sourceFs = ClientFileManager.GetFileStream(FileName);
            using var fs = new MemoryStream();
            sourceFs.CopyTo(fs);
            if (fs.Length <= 8)
            {
                // File too small contain data
                return true;
            }
            fs.Seek(0, SeekOrigin.Begin);

            using var br = new BinaryReader(fs);
            
            // Asset Paths
            var assetPathCount = br.ReadUInt32();
            for (var i = 0u; i < assetPathCount; i++)
            {
                var unknown = br.ReadUInt32();
                var chunkBytes = br.ReadBytes(256);
                var assetPath = new AssetPath
                {
                    Unknown = unknown,
                    Name = Encoding.UTF8.GetString(chunkBytes).Replace("\0", "").TrimEnd()
                };
                AssetPathsList.Add(assetPath);
            }

            // Prefabs
            var prefabCount = br.ReadUInt32();
            br.BaseStream.Seek(prefabCount * 260, SeekOrigin.Current);
            for (var i = 0u; i < prefabCount; i++)
            {
                if (br.BaseStream.Position >= br.BaseStream.Length)
                    return true;

                var (nextOffset, success) = ReadPrefabNode(br, (int)br.BaseStream.Position,">");
                if (!success)
                {
                    return false;
                }

                br.BaseStream.Position = nextOffset;
            }
            return true;
        }
        catch (Exception e)
        {
            Logger.Warn($"Error reading file {FileName}, exception: {e}");
        }
        return false;
    }

    private (long, bool) ReadPrefabNode(BinaryReader br, int readerStartOffset, string debugPrefix)
    {
        br.BaseStream.Position = readerStartOffset;
        if (br.BaseStream.Position + NodeHeaderSize > br.BaseStream.Length)
            return (br.BaseStream.Position, false);
        var unknownInt = br.ReadInt32();
        var startPos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        var endPos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        var objectDataBlockSize = br.ReadInt32();
        var childrenBitMask = br.ReadByte();
        var contentStartOffset = br.BaseStream.Position;
        var childStartOffset = contentStartOffset;

        // Logger.Debug($"{debugPrefix} Reading node at 0x{readerStartOffset:X}, Int: {unknownInt}, Pos: {startPos} -> {endPos}, Size: {objectDataBlockSize}, ChildBits: {childrenBitMask:b8}");

        if (objectDataBlockSize > 0)
        {
            if (contentStartOffset + objectDataBlockSize > br.BaseStream.Length)
                return (br.BaseStream.Length, false);
            var dataBlock = br.ReadBytes(objectDataBlockSize);
            if (!ParseObjectBlockData(br, dataBlock))
                return (br.BaseStream.Length, false);
            childStartOffset = br.BaseStream.Position;
        }

        var currentChildOffset = childStartOffset;
        if (childrenBitMask > 0)
        {
            for (int i = 0; i < 8; i++)
            {
                if ((childrenBitMask & (1 << i)) != 0)
                {
                    br.BaseStream.Position = currentChildOffset;
                    var (nextOffset, success) = ReadPrefabNode(br, (int)currentChildOffset, $"--{debugPrefix}" );
                    currentChildOffset = nextOffset;
                    if (!success)
                        return (br.BaseStream.Length, false);
                }
            }
        }

        // Logger.Debug($"{debugPrefix} Reading node at 0x{readerStartOffset:X} ended at 0x{br.BaseStream.Position:X}");
        return (currentChildOffset, true);
    }

    private bool ParseObjectBlockData(BinaryReader br, byte[] blockData)
    {
        // Logger.Debug($"Start parsing ObjectBlock @ 0x{br.BaseStream.Position:X}");
        var blockSize = blockData.Length;
        var offset = 0;
        while (offset < blockSize)
        {
            var startOfObjectOffset = offset;
            if (offset + 4 > blockSize)
                break;
            var objectType = BitConverter.ToInt32(blockData, offset);
            var totalObjectSize = ObjectTotalSizesByType.GetValueOrDefault(objectType, 0);
            switch (objectType)
            {
                case 6:
                    // Voxel
                    var numRangesOffset = offset + 198779;
                    if (numRangesOffset + 4 > blockSize)
                        return false;
                    var numRanges = BitConverter.ToInt32(blockData, numRangesOffset);

                    var type6 = new PrefabDataType6Voxel();
                    type6.PrefabType = objectType;
                    type6.Data = blockData;
                    type6.NumRanges = numRanges;

                    var currentPosInObject = numRangesOffset + 4;
                    for (var i = 0; i < numRanges; i++)
                    {
                        if (currentPosInObject + 4 > blockSize)
                            return false;
                        var dataChunkSize = BitConverter.ToInt32(blockData, currentPosInObject);
                        type6.ChunkData.Add(blockData.Skip(currentPosInObject).Take(dataChunkSize).ToArray());
                        currentPosInObject += (4 + dataChunkSize);
                    }
                    PrefabsList.Add(type6);
                    if (currentPosInObject > blockSize)
                        return false;
                    totalObjectSize = currentPosInObject - offset;
                    break;
                case 11:
                    // Water
                    if (offset + 123 > blockSize)
                        return false;
                    var flagValue = blockData.Skip(offset + 43).FirstOrDefault();
                    var arrayCount = BitConverter.ToInt32(blockData, offset + 119);

                    var type11 = new PrefabDataType11Water();
                    type11.PrefabType = objectType;
                    type11.Data = blockData;
                    type11.Flags = flagValue;
                    type11.ArrayCount = arrayCount;
                    type11.StartPos = new Vector3(BitConverter.ToSingle(blockData, 4),BitConverter.ToSingle(blockData, 8),BitConverter.ToSingle(blockData, 12));
                    type11.EndPos = new Vector3(BitConverter.ToSingle(blockData, 16),BitConverter.ToSingle(blockData, 20),BitConverter.ToSingle(blockData, 24));

                    if (flagValue == 1)
                    {
                        var entryStart = 48 + 119 + 4;
                        totalObjectSize = (arrayCount * 12) + entryStart;
                        for (var i = 0; i < arrayCount; i++)
                        {
                            type11.PointsList.Add(GetVector3(blockData, entryStart + (i * 12)));
                        }
                    }
                    else if (flagValue == 2)
                    {
                        var entryStart = 119 + 4;
                        totalObjectSize = (arrayCount * 24) + entryStart;
                        for (var i = 0; i < arrayCount; i++)
                        {
                            type11.PointsList.Add(GetVector3(blockData, entryStart + (i * 24)));
                            type11.PointsList.Add(GetVector3(blockData, entryStart + (i * 24) + 12));
                        }
                    }
                    else if (flagValue == 3)
                    {
                        var entryStart = 48 + 119 + 4;
                        totalObjectSize = (arrayCount * 12) + entryStart;
                        for (var i = 0; i < arrayCount; i++)
                        {
                            type11.PointsList.Add(GetVector3(blockData, entryStart + (i * 12)));
                        }
                    }
                    else
                    {
                        return false;
                    }

                    PrefabsList.Add(type11);
                    break;
                case 13:
                    // Road
                    if (offset + 44 > blockSize)
                        return false;
                    var countValue = blockData[offset + 43];
                    totalObjectSize = (countValue * 12) + 67;
                    var type13 = new PrefabDataType13Road();
                    type13.PrefabType = objectType;
                    type13.Data = blockData;
                    type13.ArrayCount = countValue;
                    for (var i = 0; i < countValue; i++)
                    {
                        type13.PointsList.Add(GetVector3(blockData, 67 + (i * 12)));
                    }
                    PrefabsList.Add(type13);
                    break;
                default:
                    // Others
                    if (totalObjectSize > 0)
                    {
                        var objectDataSlice = blockData.Skip(startOfObjectOffset).Take((int)totalObjectSize).ToArray();

                        var prefab = new PrefabData
                        {
                            PrefabType = objectType,
                            Data = objectDataSlice
                        };
                        PrefabsList.Add(prefab);
                        // Logger.Debug($"Added Prefab Type {prefab.PrefabType} @ 0x{startOfObjectOffset:X} Size: {totalObjectSize} / {blockSize}");
                    }
                    else
                    {
                        // Unsupported Type
                        Logger.Debug($"Unexpected type {objectType} @ 0x{br.BaseStream.Position:X}, DataOffset: 0x{startOfObjectOffset:X} in {FileName}");
                        return false;
                    }

                    break;
            }

            offset += (int)totalObjectSize;
        }

        return true;
    }

    private Vector3 GetVector3(byte[] blockData, int offset)
    {
        return new Vector3(
            BitConverter.ToSingle(blockData, offset + 0),
            BitConverter.ToSingle(blockData, offset + 4),
            BitConverter.ToSingle(blockData, offset + 8)
        );
    }
}
