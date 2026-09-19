using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Tasks.Duels;

namespace AAEmu.Game.Models.Game.Duels;

public class Duel(Character challenger, Character challenged)
{
    public Character Challenger { get; } = challenger; // This is the character who challenged us to a duel.
    public Character Challenged { get; } = challenged; // This is our character (e.g. connection.ActiveChar)
    public Doodad DuelFlag { get; set; }
    public DuelStartTask DuelStartTask { get; set; }
    public DuelEndTimerTask DuelEndTimerTask { get; set; }
    public DuelDistanceСheckTask DuelDistanceСheckTask { get; set; }
    public DuelResultСheckTask DuelResultСheckTask { get; set; }
    public bool DuelStarted { get; set; }
    public bool DuelAllowed { get; set; }

    public void SendPacketsBoth(GamePacket packet)
    {
        // This is used when both characters have the same packet data
        // Broadcast-style, only to those in a duel
        Challenger.SendPacket(packet);
        Challenged.SendPacket(packet);
    }
    public void SendPacketChallenger(GamePacket packet)
    {
        Challenger.SendPacket(packet); // only to the one who challenged him to a duel
    }
    public void SendPacketChallenged(GamePacket packet)
    {
        Challenged.SendPacket(packet); // only to someone challenged to a duel
    }
}
