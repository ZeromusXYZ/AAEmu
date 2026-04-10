using System.Drawing;
using AAEmu.Game.Utils;
using NLog;

namespace AAEmu.Game.Models.ClientData;

public class NodeCell()
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger(); 
    private const int Inv5Cm = 20;
    private const uint Mask12Bit = (1 << 12) - 1;
    private const byte HeightMapMaterialBitsCount = 5;
    public const ushort HeightMapValueBits = 0b_1111_1111_1110_0000;
    public const ushort HeightMapMaterialBits = 0b_0000_0000_0001_1111;
    public const byte HeightMapMaterialHole = 0b_0001_1111;
    public const ushort ShiftedHeightDataMaxValue = ushort.MaxValue >> HeightMapMaterialBitsCount;

    public byte Version { get; set; }
    public byte Dummy { get; set; }
    public byte Flags { get; set; }
    public byte Flags2 { get; set; }
    public AABB BoxHeightmap { get; set; } = new ();
    public byte NodeHasHoles { get; private set; }
    private float FOffset { get; set; }
    public float FRange { get; private set; }
    public int NodeSize { get; private set; }
    private ushort[] RawHeightMapData { get; set; }
    /// <summary>
    /// Processed (upscaled) converted actual height values
    /// </summary>
    public float[,] HeightData { get; init; } = new float[33, 33];
    /// <summary>
    /// Processed (to match upscaling) converted material Id for the points in HeightData
    /// </summary>
    public byte[,] MaterialData { get; init; } = new byte[33, 33];

    private int IOffset { get; set; }
    private int IRange { get; set; }
    private int IStep { get; set; }
    private float FMin { get; set; }
    private float FMax { get; set; }
    private double DoubleValue { get; set; }

    public void Read(BinaryReader br)
    {
        Version = br.ReadByte();
        Dummy = br.ReadByte();
        Flags = br.ReadByte();
        Flags2 = br.ReadByte();

        BoxHeightmap.Min.X = br.ReadSingle();
        BoxHeightmap.Min.Y = br.ReadSingle();
        BoxHeightmap.Min.Z = br.ReadSingle();
        BoxHeightmap.Max.X = br.ReadSingle();
        BoxHeightmap.Max.Y = br.ReadSingle();
        BoxHeightmap.Max.Z = br.ReadSingle();

        NodeHasHoles = br.ReadByte();
        FOffset = br.ReadSingle();

        FRange = br.ReadSingle();
        NodeSize = br.ReadInt32();
        RawHeightMapData = new ushort[NodeSize * NodeSize];

        var unkCount = br.ReadInt32();

        for (var i = 0; i < RawHeightMapData.Length; i++)
            RawHeightMapData[i] = br.ReadUInt16();

        br.ReadInt32();
        br.ReadSingle();
        br.ReadSingle();
        br.ReadSingle();
        br.ReadSingle();

        br.ReadBytes(36 + unkCount);

        Init();
        ConvertTo33By33();
    }

    private float RawDataToHeight(ushort data)
    {
        var shiftedVal = data >> HeightMapMaterialBitsCount;
        var d = (float)shiftedVal / (float)ShiftedHeightDataMaxValue;
        return float.Lerp(BoxHeightmap.Min.Z, BoxHeightmap.Max.Z, d);
        // return 0.05f * IOffset + (data >> HeightMapMaterialBitsCount) * IStep * 0.05f;
    }

    public ushort RawDataByIndex(ushort nX, ushort nY)
    {
        if (NodeSize > 0)
        {
            var index = nX * NodeSize + nY;
            if (index >= RawHeightMapData.Length)
                return 0;

            return RawHeightMapData[index];
        }

        return 0;
    }

    private void Init()
    {
        FMin = FOffset;
        FMax = FMin + 0xFFF0 * FRange;

        IOffset = (int)(FMin * Inv5Cm);
        IRange = (int)((FMax - FMin) * Inv5Cm);
        IStep = (int)(IRange > 0 ? (IRange + Mask12Bit - 1) / Mask12Bit : 1);
        DoubleValue = FRange * 100000d;
    }

    private Rectangle FindNearestSignificantPoints(int x, int y)
    {
        return new Rectangle(x / NodeSize, y / NodeSize, 1, 1);
    }

    private ushort GetRawHeight(int x, int y)
    {
        return (ushort)(RawDataByIndex((ushort)x, (ushort)y) & HeightMapValueBits);
    }

    private byte GetRawMaterial(int x, int y)
    {
        return (byte)(RawDataByIndex((ushort)x, (ushort)y) & HeightMapMaterialBits);
    }

    /// <summary>
    /// Make sure that the target "resolution" is 33x33
    /// </summary>
    private void ConvertTo33By33()
    {
        switch (NodeSize)
        {
            case > 0 and < 33:
                {
                    var sourceScale = NodeSize / 33f;

                    for (var targetX = 0; targetX <= 32; targetX++)
                    for (var targetY = 0; targetY <= 32; targetY++)
                    {
                        // var index = targetX * 33 + targetY;

                        var sourceX = (ushort)Math.Floor(targetX * sourceScale);
                        var sourceY = (ushort)Math.Floor(targetY * sourceScale);

                        var nearestRawPoints = FindNearestSignificantPoints(sourceX, sourceY);

                        // Get heights for these points
                        var rawHeightTl = RawDataToHeight(GetRawHeight(nearestRawPoints.Left, nearestRawPoints.Top));
                        var rawHeightTr = RawDataToHeight(GetRawHeight(nearestRawPoints.Right, nearestRawPoints.Top));
                        var rawHeightBl = RawDataToHeight(GetRawHeight(nearestRawPoints.Left, nearestRawPoints.Bottom));
                        var rawHeightBr = RawDataToHeight(GetRawHeight(nearestRawPoints.Right, nearestRawPoints.Bottom));

                        // Calculate offset within points
                        var offX = (targetX * sourceScale) - sourceX;
                        var offY = (targetY * sourceScale) - sourceY;

                        // Save into the actually used array
                        HeightData[targetX, targetY] = MathUtil.Blerp(rawHeightTl, rawHeightTr, rawHeightBl, rawHeightBr, offX, offY);

                        // "Merge" the materials flags and use that as a materials
                        // If one of them is a hole (0x1F), they the result should also be a hole, just don't use them to draw the texture of the floor with it :)
                        // TODO: Verify if it's actually flags and not values
                        var averageMaterial =
                            GetRawMaterial(nearestRawPoints.Left, nearestRawPoints.Top) |
                            GetRawMaterial(nearestRawPoints.Right, nearestRawPoints.Top) |
                            GetRawMaterial(nearestRawPoints.Left, nearestRawPoints.Bottom) |
                            GetRawMaterial(nearestRawPoints.Right, nearestRawPoints.Bottom);
                        MaterialData[targetX, targetY] = (byte)averageMaterial;
                    }
                    break;
                }
            case 0:
                {
                    for (var targetX = 0; targetX <= 32; targetX++)
                    for (var targetY = 0; targetY <= 32; targetY++)
                    {
                        HeightData[targetX, targetY] = 0f; // TODO: does this need to be the minimum height of the box?
                        MaterialData[targetX, targetY] = NodeHasHoles > 0 ? HeightMapMaterialHole : (byte)0;
                    }

                    break;
                }
            case 33:
                {
                    for (var targetX = 0; targetX <= 32; targetX++)
                    for (var targetY = 0; targetY <= 32; targetY++)
                    {
                        HeightData[targetX, targetY] = RawDataToHeight(GetRawHeight(targetX, targetY));
                        MaterialData[targetX, targetY] = GetRawMaterial(targetX, targetY);
                    }
                }
                break;
            case > 33:
                Logger.Fatal($"Unsupported node size: {NodeSize}");
                break;
        }
    }

    /// <summary>
    /// Used for node sorting only, don't rely directly on this function
    /// </summary>
    /// <returns></returns>
    public bool HasData()
    {
        return RawHeightMapData.Length > 0;
    }
}
