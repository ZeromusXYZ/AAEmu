namespace AAEmu.Game.Models.Game.GameConfigs;

public class ContentConfig
{
    /// <summary>Content ID number</summary>
    public ContentConfigEnum Id { get; init; }
    /// <summary>Not sure what kind represents here, in later versions this value is not present</summary>
    public uint KindId { get; init; }
    /// <summary>Raw value for this content setting</summary>
    public int Value { get; init; } 
}

/*
    KindId seems to be a category
    1   labor ticks and init values
    3   trading
    4   used by MaxSimCount with a value of 5. Possibly chunk size?
    5   auction
    6   quests
    7   labor, adjusted pc_bang values?
    8   duels
    11  housing (sale)
    12  skill change
 */
