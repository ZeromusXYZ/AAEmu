using System.Numerics;

namespace AAEmu.Game.Models.Game.AI.AStar;

public class Vector3WithType(Vector3 position, byte nodeType)
{
    public static Vector3WithType Zero { get; } = new Vector3WithType(Vector3.Zero, 0);

    public Vector3 Position { get; init; } = position;
    public byte NodeType { get; init; } = nodeType;
}
