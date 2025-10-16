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
            
            /*
            // ImHex object.dat pattern file
            struct AssetPathStruct {
                u32 Unknown;
                char AssetPath[256];
            };

            struct PrefabPathStruct {
                u32 Unknown;
                char AssetPath[256];
            };

            struct Vector3 {
                float x;
                float y;
                float z;
            };

            u32 AssetPathCount @ 0x00;
            AssetPathStruct AssetPaths[AssetPathCount] @ 0x04;

            u32 prefabStart = 0x04 + (AssetPathCount * sizeof(AssetPathStruct));
            u32 PrefabCount @ prefabStart;
            PrefabPathStruct PrefabPaths[PrefabCount] @ prefabStart + 0x04;
            u32 prefabBlockStart = prefabStart + (PrefabCount * sizeof(PrefabPathStruct)) + 0x04;

            // Read PrefabNode
            u32 PrefabNodeStart = prefabBlockStart;
            u32 UnknownInt @ PrefabNodeStart;
            Vector3 StartPos @ PrefabNodeStart + 0x04;
            Vector3 EndPos @ PrefabNodeStart + 0x10;
            u32 ObjectDataBlockSize @ PrefabNodeStart + 0x1C;
            u8 ChildrenBitMask @ PrefabNodeStart + 0x20;
            */

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
                    type6.Data = [];
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
                    totalObjectSize = currentPosInObject - offset;
                    type6.Data = blockData.Skip(offset).Take(totalObjectSize).ToArray();
                    PrefabsList.Add(type6);
                    if (currentPosInObject > blockSize)
                        return false;
                    break;
                case 11:
                    // Water
                    const int StartOfVariableData = 0x7B; // Variable points data starts at this offset
                    if (offset + StartOfVariableData > blockSize)
                        return false; // Minimum required size to possibly be valid 

                    var type11 = new PrefabDataType11Water();
                    /*
                        // ImHex pattern for Prefab Type 11 (water)
                        struct Vector3 {
                            float x;
                            float y;
                            float z;
                        };

                        struct Color {
                            u8 r;
                            u8 g;
                            u8 b;
                            u8 a;
                        } [[hex::inline_visualize("color", r, g, b, a)]];

                        u32 PrefabType @ 0x00;
                        Vector3 StartPos @ 0x04;
                        Vector3 EndPos @ 0x10;
                        Vector3 vector_at_0x1C @ 0x1C; // unknown Vector3
                        u8 byte_100_at_0x28 @ 0x28; // seems to always be 100
                        u8 byte_100_at_0x29 @ 0x29; // seems to always be 100
                        u8 byte_at_0x2A @ 0x2A; // unknown
                        u32 Flag @ 0x2B;
                        u32 u32_at_0x2F_id__or_more_flags_maybe @ 0x2F; // unknown data
                        float float_at_0x33 @ 0x33; // unknown data
                        float float_at_0x37 @ 0x37; // unknown data
                        float quaternion_at_0x3B[0x04] @ 0x3B; // unknown data, might not be a quaternion
                        Vector3 normal_vector_at_0x4B @ 0x4B; // looks like the normal vector for the surface
                        float vector3_at_0x57[0x03] @ 0x57; // unknown data
                        float vector2_at_0x63[0x02] @ 0x63; // unknown data
                        u32 ArrayCount1 @ 0x6B;
                        Color maybe_color_1 @ 0x6F; // Looks like a color multiplier RGBA
                        Color maybe_color_2 @ 0x73; // Looks like a color multiplier RGBA
                        u32 ArrayCount2 @ 0x77;
                        Vector3 SegmentDataPoints[ArrayCount1] @ 0x7B;
                        Vector3 BorderDataPoints[ArrayCount2] @ 0x7B + (ArrayCount1 * 12);
                    */
                    type11.Data = []; // Default to blank data 
                    type11.PrefabType = objectType; // u @ 0x00
                    type11.StartPos = GetVector3(blockData, offset + 0x04); // 3 x float @ 0x04 
                    type11.EndPos =  GetVector3(blockData, offset + 0x10);  //
                    type11.Flags = blockData.Skip(offset + 0x2B).FirstOrDefault();
                    type11.ArrayCount1 = BitConverter.ToInt32(blockData, offset + 0x6B);
                    type11.ArrayCount2 = BitConverter.ToInt32(blockData, offset + 0x77);

                    totalObjectSize = (type11.ArrayCount1 * 12) + (type11.ArrayCount2 * 12) + StartOfVariableData;

                    // Read points for inside data
                    for (var i = 0; i < type11.ArrayCount1; i++)
                        type11.PointsList1.Add(GetVector3(blockData, offset + StartOfVariableData + (i * 12)));

                    // Read border data
                    var entryStart2 = StartOfVariableData + (type11.ArrayCount1 * 12);
                    for (var i = 0; i < type11.ArrayCount2; i++)
                        type11.BorderPointsList.Add(GetVector3(blockData, offset + entryStart2 + (i * 12)));

                    if ((type11.ArrayCount1 <= 0) && (type11.ArrayCount2 <= 0))
                        return true;

                    type11.Data = blockData.Skip(offset).Take(totalObjectSize).ToArray();
                    if (type11.Data.Length != totalObjectSize)
                    {
                        Logger.Warn($"Size mismatch while reading {FileName} in block @ {br.BaseStream.Position}, {type11.Data.Length} != {totalObjectSize}");
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
                    type13.ArrayCount = countValue;
                    for (var i = 0; i < countValue; i++)
                    {
                        type13.PointsList.Add(GetVector3(blockData, 67 + (i * 12)));
                    }
                    type13.Data = blockData.Skip(offset).Take(totalObjectSize).ToArray();
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
