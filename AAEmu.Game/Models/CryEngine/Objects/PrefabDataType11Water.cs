using System.Numerics;

namespace AAEmu.Game.Models.CryEngine.Objects;

public class PrefabDataType11Water : PrefabData
{
    public int Flags { get; set; }
    public int ArrayCount { get; set; }
    public List<Vector3> PointsList { get; set; } = [];
    public Vector3 StartPos { get; set; } = Vector3.Zero;
    public Vector3 EndPos { get; set; } = Vector3.Zero;
}
