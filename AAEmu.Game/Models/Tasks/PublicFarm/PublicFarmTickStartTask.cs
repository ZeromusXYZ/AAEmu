using AAEmu.Game.Core.Managers;

namespace AAEmu.Game.Models.Tasks.PublicFarm;

public class PublicFarmTickStartTask(PublicFarmManager farmManager) : Task
{
    public override void Execute()
    {
        farmManager?.PublicFarmTick();
    }
}
