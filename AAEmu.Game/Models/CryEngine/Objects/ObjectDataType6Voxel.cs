using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using CgfConverter.Structs;

namespace AAEmu.Game.Models.CryEngine.Objects;

public class ObjectDataType6Voxel() : ObjectDataBase(6)
{
    public List<byte[]> ChunkData { get; set; } = [];

    // Main Header
    public Vector3 BoundingBoxMin { get; set; }
    public Vector3 BoundingBoxMax { get; set; }
    public int UnknownPadding1 { get; set; }
    public float CullDistance { get; set; }
    public int BitMask { get; set; }
    public byte ViewDistanceRatio { get; set; }
    public byte LodRatio { get; set; }
    public byte UnknownPadding2 { get; set; }

    // Voxel Header

    public int VoxelChunkType { get; set; }
    public int VoxelExtraX { get; set; }
    public int VoxelExtraY { get; set; }
    public int VoxelExtraZ { get; set; }
    public int[] VoxelResolution { get; set; } = new int[3];
    public int VoxelUnknown { get; set; }

    // Voxel Data Channels
    public byte[] ProcessedChunk1 { get; set; } = new byte[65536]; // 65536 bytes
    public byte[] MaterialNamesData { get; set; } = new byte[2048]; // 2048 bytes
    public byte[] ProcessedChunk2 { get; set; } = new byte[131072]; // 131072 bytes

    public Matrix3x4 ModelTransformMatrix { get; set; }
    public int ModelNumLods { get; set; }

    // Data
    public int LodCompressedSize { get; set; }
    public int LodUnCompressedSize { get; set; }
    public byte[] CompressedModelData { get; set; } = [];
    public System.IO.Stream DecompressedModelData { get; set; } = new MemoryStream();

    public VoxelMeshReader? MeshReader { get; private set; }
    public VoxelMeshProcessor? MeshProcessor { get; private set; }

    public bool Contains(Vector3 point, bool ignoreZ = false)
    {
        return point.X >= BoundingBoxMin.X && point.X <= BoundingBoxMax.X &&
               point.Y >= BoundingBoxMin.Y && point.Y <= BoundingBoxMax.Y &&
               (ignoreZ || (point.Z >= BoundingBoxMin.Z && point.Z <= BoundingBoxMax.Z));
    }

    public bool ReadData()
    {
        try
        {
            var currentOffset = 0;

            {
                // Main Header
                var objectType = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
                BoundingBoxMin = new Vector3(BitConverter.ToSingle(Data, currentOffset), BitConverter.ToSingle(Data, currentOffset + 4), BitConverter.ToSingle(Data, currentOffset + 8)); currentOffset += 12;
                BoundingBoxMax = new Vector3(BitConverter.ToSingle(Data, currentOffset), BitConverter.ToSingle(Data, currentOffset + 4), BitConverter.ToSingle(Data, currentOffset + 8)); currentOffset += 12;
                UnknownPadding1 = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
                CullDistance = BitConverter.ToSingle(Data, currentOffset); currentOffset += 4;
                BitMask = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
                ViewDistanceRatio = Data[currentOffset]; currentOffset += 1;
                LodRatio = Data[currentOffset]; currentOffset += 1;
                UnknownPadding2 = Data[currentOffset]; currentOffset += 1;

                // Voxel Header
                VoxelChunkType = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
                VoxelExtraX = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
                VoxelExtraY = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
                VoxelExtraZ = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
                VoxelResolution = new int[3] { BitConverter.ToInt32(Data, currentOffset), BitConverter.ToInt32(Data, currentOffset + 4), BitConverter.ToInt32(Data, currentOffset + 8) }; currentOffset += 12;
                VoxelUnknown = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;

                // Voxel Data Channels
                ProcessedChunk1 = Data.Skip(currentOffset).Take(65536).ToArray(); currentOffset += 65536;
                MaterialNamesData = Data.Skip(currentOffset).Take(2048).ToArray(); currentOffset += 2048;
                ProcessedChunk2 = Data.Skip(currentOffset).Take(131072).ToArray(); currentOffset += 131072;

                ModelTransformMatrix = GetMatrix3X4(Data, currentOffset);
                /*
                ModelTransformMatrix = new float[12] {
                BitConverter.ToSingle(Data, currentOffset),      BitConverter.ToSingle(Data, currentOffset + 4),  BitConverter.ToSingle(Data, currentOffset + 8),
                BitConverter.ToSingle(Data, currentOffset + 12), BitConverter.ToSingle(Data, currentOffset + 16), BitConverter.ToSingle(Data, currentOffset + 20),
                BitConverter.ToSingle(Data, currentOffset + 24), BitConverter.ToSingle(Data, currentOffset + 28), BitConverter.ToSingle(Data, currentOffset + 32),
                BitConverter.ToSingle(Data, currentOffset + 36), BitConverter.ToSingle(Data, currentOffset + 40), BitConverter.ToSingle(Data, currentOffset + 44) };
                */
                currentOffset += 48;
                ModelNumLods = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
            }
            LodCompressedSize = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
            LodUnCompressedSize = BitConverter.ToInt32(Data, currentOffset); currentOffset += 4;
            var dataSizetoRead = LodCompressedSize - 4;
            if (dataSizetoRead < 0)
            {
                // Compressed Voxel data size is smaller than expected.
                return false;
            }
            if (Data.Length < currentOffset + dataSizetoRead)
            {
                // Not enough data to read compressed voxel data.
                return false;
            }
            var endOffset = currentOffset + dataSizetoRead;
            CompressedModelData = Data.Skip(currentOffset).Take(dataSizetoRead).ToArray();



            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error compiling voxel data: {ex.Message}");
            return false;
        }

        /*
         * TYPE_6_STRUCTURE = [
        # --- Main Header (43 bytes) ---
        ('object_type',           4),   # int32: Should be 6
        ('bounding_box',          24),  # 6 floats start bounding box and end boundbox pos
        ('unknown_padding_1',     4),
        ('cull_distance',         4),   # float
        ('bitmask',               4),   # int32
        ('view_distance_ratio',   1),   # int8
        ('lod_ratio',             1),   # int8
        ('unknown_padding_2',     1),

        # --- Voxel Header (32 bytes) ---
        ('voxel_chunk_type',      4),
        ('voxel_extra_x',         4),   # int32: Will be used by voxel_string.py
        ('voxel_extra_y',         4),   # int32: Will be used by voxel_string.py
        ('voxel_extra_z',         4),   # int32: Will be used by voxel_string.py
        ('voxel_resolution',      12),
        ('voxel_unknown',         4),

        # --- Voxel Data Channels (The old 'compression_bytes') ---
        ('processed_chunk_1',     65536),
        ('material_names',        2048),  # Will be used by voxel_string.py
        ('processed_chunk_2',     131072),

        # --- Model Header (52 bytes) ---
        ('model_transform_matrix', 48),
        ('model_num_lods',        4),    # int32
        ]
        */
    }

    public bool ExportData(string fileName)
    {
        if (!Parse())
        {
            // Wasn't able to generate any data
            return false;
        }

        if (MeshReader == null)
        {
            return false;
        }

        // CreateVoxelModel(voxelMaker);        
        var objFileSb = new System.Text.StringBuilder();
        objFileSb.AppendLine($"# Exported Voxel Mesh");
        objFileSb.AppendLine($"# Vertices: {MeshReader.Vertices.Count}");
        objFileSb.AppendLine($"# Faces: {MeshReader.Indices.Count / 3}");
        foreach (var vertex in MeshReader.Vertices)
        {
            // Source vertices were loaded as -X, Z, Y format already
            objFileSb.AppendLine($"v {(vertex.X).ToString(CultureInfo.InvariantCulture)} {vertex.Y.ToString(CultureInfo.InvariantCulture)} {vertex.Z.ToString(CultureInfo.InvariantCulture)}");
        }
        for (int i = 0; i < MeshReader.Indices.Count; i += 3)
        {
            var f1 = MeshReader.Indices[i] + 1;
            var f2 = MeshReader.Indices[i + 1] + 1;
            var f3 = MeshReader.Indices[i + 2] + 1;
            objFileSb.AppendLine($"f {f1} {f2} {f3}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fileName) ?? "."); 
        File.WriteAllText(fileName, objFileSb.ToString());

        return true;
    }

    public bool Parse()
    {
        if (CompressedModelData == null || CompressedModelData.Length <= 0)
            return false;

        if (MeshProcessor != null)
            return true; // Already parsed

        // Decompress the model data
        var decompressionStream = new ZLibStream(new MemoryStream(CompressedModelData), CompressionMode.Decompress);
        using (var memStream = new MemoryStream())
        {
            DecompressedModelData.SetLength(0);
            decompressionStream.CopyTo(DecompressedModelData);
        }

        // Validate size
        if (DecompressedModelData.Length != LodUnCompressedSize)
        {
            // Size mismatch for decompressed data
            return false;
        }

        // Make reader and parse headers/chunks
        MeshReader = new VoxelMeshReader(DecompressedModelData);
        if (!MeshReader.Parse())
        {
            // Mesh reader failed to parse
            return false;
        }

        // Process the mesh from the reader
        MeshProcessor = new VoxelMeshProcessor(MeshReader);
        if (MeshProcessor == null)
        {
            // Mesh processor failed to process
            return false;
        }

        if (MeshProcessor.Process() == false)
        {
            // Mesh processor failed to process
            return false;
        }

        return true;
    }
}
