namespace AAEmu.Game.Models.CryEngine.Objects;

public class PrefabDataType6Voxel() : PrefabDataBase(6)
{
    public override bool IsGeneric { get; protected init; } = false;

    public int NumRanges { get; private set; }
    public List<byte[]> ChunkData { get; private set; } = [];
    
    public override int ReadData(byte[] blockData, int offset)
    {
        var numRangesOffset = offset + 198779;

        var objectType = BitConverter.ToInt32(blockData, offset + 0x00);
        if (objectType != PrefabType || (numRangesOffset + 4 > blockData.Length))
        {
            // Type mismatch or not enough bytes, return as error
            Data = [];
            return 0;
        }

        NumRanges = BitConverter.ToInt32(blockData, numRangesOffset);
        var currentPosInObject = numRangesOffset + 4;
        for (var i = 0; i < NumRanges; i++)
        {
            if (currentPosInObject + 4 > blockData.Length)
            {
                Data = [];
                return 0;
            }
            var dataChunkSize = BitConverter.ToInt32(blockData, currentPosInObject);
            ChunkData.Add(blockData.Skip(currentPosInObject).Take(dataChunkSize).ToArray());
            currentPosInObject += (4 + dataChunkSize);
        }
        var totalObjectSize = currentPosInObject - offset;
        Data = blockData.Skip(offset).Take(totalObjectSize).ToArray();
        return Data.Length;
    }
}
