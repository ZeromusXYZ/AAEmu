using AAEmu.Game.Models.Game.GameConfigs;

namespace AAEmu.Game.Core.Managers;

public interface IGameContentManager : ILoadable
{
    ContentConfig GetContentConfig(uint contentId);
}
