using System.Numerics;

namespace AAEmu.Game.Models.CryEngine.Objects;

public class PrefabDataType13Road : PrefabData
{
    public int ArrayCount { get; set; }
    public List<Vector3> PointsList { get; set; } = [];
}
