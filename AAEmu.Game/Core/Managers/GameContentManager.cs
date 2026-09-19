using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game.GameConfigs;
using AAEmu.Game.Utils.DB;

namespace AAEmu.Game.Core.Managers;

public class GameContentManager : Singleton<GameContentManager>, IGameContentManager
{

    private Dictionary<uint, ContentConfig> _contentConfig;
    private bool _loaded = false;

    // Getters
    public ContentConfig GetContentConfig(uint contentId)
    {
        return _contentConfig.GetValueOrDefault(contentId);
    }

    public void Load()
    {
        if (_loaded)
            return;

        _contentConfig = new Dictionary<uint, ContentConfig>();

        using (var connection = SQLite.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM content_configs";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var config = new ContentConfig()
                        {
                            Id = reader.GetUInt32("id"),
                            KindId = reader.GetUInt32("kind_id"),
                            Value = reader.GetInt32("value"),
                        };

                        _contentConfig.TryAdd(config.Id, config);
                    }
                }
            }

        }

        _loaded = true;
    }
}
