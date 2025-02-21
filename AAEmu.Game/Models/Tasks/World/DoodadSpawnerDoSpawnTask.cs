using AAEmu.Game.Models.Game.DoodadObj;

namespace AAEmu.Game.Models.Tasks.World;

public class DoodadSpawnerDoSpawnTask(DoodadSpawner doodadSpawner, Doodad doodad) : Task
{
    public override void Execute()
    {
        doodadSpawner.DoSpawn(doodad);
    }
}
