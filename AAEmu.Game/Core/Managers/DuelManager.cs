using System.Collections.Concurrent;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Duels;
using AAEmu.Game.Models.Game.GameConfigs;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Models.Tasks.Duels;
using AAEmu.Game.Utils;

using NLog;

namespace AAEmu.Game.Core.Managers;

public class DuelManager(IGameContentManager gameContentManager) : Singleton<DuelManager>, IDuelManager
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private DoodadSpawner _combatFlag;
    private const double Delay = 1000; // 1 sec
    private float DistanceForSurrender { get; } = gameContentManager.GetContentConfig(ContentConfigEnum.DuelDistance).Value; // 100m
    private const double DuelDurationTime = 5; // 5 min
    private const uint DuelFlagDoodadId = 5014;
    private const uint DuelBubbleChatType = 543;

    // there can be several duels at the same time
    private readonly ConcurrentDictionary<uint, Duel> _duels = new();
    public Dictionary<uint, FactionsEnum> SaveFactions { get; set; } = [];

    public void Initialize()
    {
        Logger.Info("Initialising Duel Manager...");
    }

    private void DuelAdd(Duel duel)
    {
        if (!_duels.ContainsKey(duel.Challenger.Id))
            _duels.TryAdd(duel.Challenger.Id, duel);
        if (!_duels.ContainsKey(duel.Challenged.Id))
            _duels.TryAdd(duel.Challenged.Id, duel);
    }

    private void DuelRemove(Duel duel)
    {
        _duels.TryRemove(duel.Challenger.Id, out _);
        _duels.TryRemove(duel.Challenged.Id, out _);
    }

    public void DuelRequest(Character challenger, uint challengedId)
    {
        // The ID of the person challenged to a duel is displayed
        var challenged = WorldManager.Instance.GetCharacterById(challengedId);
        var duel = new Duel(challenger, challenged);
        DuelAdd(duel);
        challenged.SendPacket(new SCDuelChallengedPacket(challenger.Id)); // we send only to the enemy
        Logger.Warn($"DuelRequest: challenger={challenger.Id}:{challenger.ObjId}, challenged={challengedId}:{challenged.Id}:{challenged.ObjId}");
    }

    public void DuelAccepted(Character challenged, uint challengerId)
    {
        ArgumentNullException.ThrowIfNull(challenged);
        // The ID of the person who challenged them to a duel is returned
        try
        {
            var duel = _duels[challengerId];

            if (!duel.DuelStarted)
            {
                duel.DuelStarted = true;
                duel.Challenger.IsInDuel = true;
                duel.Challenged.IsInDuel = true;

                // spawn flag
                _combatFlag = new DoodadSpawner
                {
                    ParentWorld = challenged.ParentWorld, Id = 0, UnitId = DuelFlagDoodadId, // Combat Flag Id=5014;
                    Position = duel.Challenger.Transform.CloneAsSpawnPosition()
                };
                _combatFlag.Position.X = duel.Challenger.Transform.World.Position.X - (duel.Challenger.Transform.World.Position.X - duel.Challenged.Transform.World.Position.X) / 2;
                _combatFlag.Position.Y = duel.Challenger.Transform.World.Position.Y - (duel.Challenger.Transform.World.Position.Y - duel.Challenged.Transform.World.Position.Y) / 2;
                _combatFlag.Position.Z = challenged.ParentWorld.Template.GeoData.GetHeight(_combatFlag.Position.AsPositionVector());

                duel.DuelFlag = _combatFlag.Spawn(0); // set CombatFlag

                // change the faction temporarily
                // TODO: Handle pets/vehicle factions, needs to be handled by player's SetFaction
                SetFaction(duel.Challenger, FactionsEnum.RedTeam);
                SetFaction(duel.Challenged, FactionsEnum.BlueTeam);

                // Schedule duel start task.
                duel.DuelStartTask = new DuelStartTask(duel.Challenger.Id);
                TaskManager.Instance.Schedule(duel.DuelStartTask, TimeSpan.FromSeconds(3));
            }
            else
                Logger.Warn($"DuelAccepted: Duel with challengerId = {challengerId} is already started");
        }
        catch (Exception e)
        {
            // id is missing in the database
            Logger.Warn($"DuelAccepted: Id = {challengerId} not found in duels[], error code: {e}");
        }
    }

    private void SetFaction(Character owner, FactionsEnum factionId)
    {
        // change the faction temporarily
        SaveFactions[owner.Id] = owner.Faction.Id;

        owner.SetFaction(factionId);
    }

    private void RestoreFaction(Character owner)
    {
        // restore the fraction
        owner.SetFaction(SaveFactions[owner.Id]);
        SaveFactions.Remove(owner.Id);
    }

    public void DuelStart(uint id)
    {
        try
        {
            var duel = _duels[id];
            duel.SendPacketsBoth(new SCDuelStartedPacket(duel.Challenger.ObjId, duel.Challenged.ObjId));
            duel.SendPacketsBoth(new SCAreaChatBubblePacket(true, duel.Challenger.ObjId, DuelBubbleChatType));
            //duel.SendPacketChallenger(new SCAreaChatBubblePacket(true, duel.Challenged.ObjId, 543));
            duel.SendPacketsBoth(new SCDuelStartCountdownPacket());
            duel.SendPacketsBoth(new SCDuelStatePacket(duel.Challenger.ObjId, duel.DuelFlag.ObjId));
            duel.SendPacketsBoth(new SCDuelStatePacket(duel.Challenged.ObjId, duel.DuelFlag.ObjId));
            // make the flag flutter in the wind
            duel.SendPacketChallenger(new SCDoodadPhaseChangedPacket(_combatFlag.Last));
            // Player can be attacked
            duel.SendPacketsBoth(new SCCombatEngagedPacket(duel.Challenger.ObjId));
            duel.SendPacketsBoth(new SCCombatEngagedPacket(duel.Challenged.ObjId));

            // final operations after a duel
            duel.DuelEndTimerTask = new DuelEndTimerTask(duel, duel.Challenger.Id);
            TaskManager.Instance.Schedule(duel.DuelEndTimerTask, TimeSpan.FromMinutes(DuelDurationTime));

            // Let's run a distance check
            _ = DuelDistanceСheck(duel.Challenger.Id);

            // Let's run a check on the health values
            _ = DuelResultСheck(duel.Challenger.Id);
        }
        catch (Exception e)
        {
            // id is missing in the database
            Logger.Warn($"DuelStart: Id = {id} not found in duels[], error code: {e}");
        }
    }

    public void DuelCancel(uint challengerId, ErrorMessageType errorMessage)
    {
        try
        {
            var duel = _duels[challengerId];
            duel.DuelAllowed = false;
            if (errorMessage != 0)
                duel.Challenger.SendErrorMessage(errorMessage);

            Logger.Warn($"DuelCancel: Duel with challengerId={challengerId} canceled, error={errorMessage}");
            DuelCleanUp(challengerId);
        }
        catch (Exception e)
        {
            // id is missing in the database
            Logger.Warn($"DuelCancel: Id={challengerId} not found in duels[], error code: {e}");
        }
    }

    private void DuelCleanUp(uint id)
    {
        try
        {
            var duel = _duels[id];

            duel.Challenger.IsInDuel = false;
            duel.Challenged.IsInDuel = false;

            if (duel.DuelStartTask != null)
            {
                _ = duel.DuelStartTask.Cancel();
                duel.DuelStartTask = null;
            }

            if (duel.DuelEndTimerTask != null)
            {
                _ = duel.DuelEndTimerTask.Cancel();
                duel.DuelEndTimerTask = null;
            }

            DuelRemove(duel);
        }
        catch (Exception e)
        {
            // id is missing in the database
            Logger.Warn($"CleanUpDuel: Id={id} not found in duels[], error code: {e}");
        }
    }

    public void DuelStop(uint id, DuelDetType det, uint loseId = 0)
    {
        try
        {
            Logger.Warn("DuelStop: Duel ended");
            var duel = _duels[id];
            duel.DuelAllowed = false;
            // Duel is over, det 00=lose, 01=win, 02=surrender (Fled beyond the flag action border), 03=draw
            if (det == DuelDetType.Draw)
            {
                duel.SendPacketChallenged(new SCDuelEndedPacket(duel.Challenger.Id, duel.Challenged.Id, duel.Challenger.ObjId, duel.Challenged.ObjId, det));
                duel.SendPacketChallenger(new SCDuelEndedPacket(duel.Challenged.Id, duel.Challenger.Id, duel.Challenged.ObjId, duel.Challenger.ObjId, det));
                Logger.Warn("DuelStop: Draw!");
            }
            else if (loseId != 0)
            {
                if (loseId == duel.Challenger.Id)
                {
                    duel.SendPacketsBoth(new SCDuelEndedPacket(duel.Challenged.Id, duel.Challenger.Id, duel.Challenged.ObjId, duel.Challenger.ObjId, det));
                    Logger.Warn($"DuelStop: Challenger:{duel.Challenger.Name} Lose, Challenged:{duel.Challenged.Name} Win!");
                }
                else if (loseId == duel.Challenged.Id)
                {
                    duel.SendPacketsBoth(new SCDuelEndedPacket(duel.Challenger.Id, duel.Challenged.Id, duel.Challenger.ObjId, duel.Challenged.ObjId, det));
                    Logger.Warn($"DuelStop: Challenger:{duel.Challenger.Name} Win, Challenged:{duel.Challenged.Name} Lose!");
                }
            }
            // Duel Status - Duel ended
            duel.SendPacketsBoth(new SCDuelStatePacket(duel.Challenged.ObjId, 0));
            duel.SendPacketsBoth(new SCDuelStatePacket(duel.Challenger.ObjId, 0));

            if (duel.DuelFlag != null)
            {
                duel.DuelFlag.Delete(); // Remove the Duel Flag
                duel.SendPacketsBoth(new SCDoodadRemovedPacket(duel.DuelFlag.ObjId));
            }

            // restore the fraction
            RestoreFaction(duel.Challenger);
            RestoreFaction(duel.Challenged);

            // Player cannot be attacked
            duel.Challenger.IsInBattle = false;
            duel.Challenged.IsInBattle = false;

            DuelCleanUp(id);
        }
        catch (Exception e)
        {
            // id is missing in the database
            Logger.Warn($"DuelStop: Id={id} not found in duels[], error code: {e}");
        }
    }

    public bool DuelResultСheck(uint id)
    {
        try
        {
            var duel = _duels[id];
            if (duel.Challenger.Hp <= 1 || duel.Challenged.Hp <= 1)
            {
                duel.DuelResultСheckTask.Cancel();
                duel.DuelResultСheckTask = null;
                return true;
            }

            duel.DuelResultСheckTask = new DuelResultСheckTask(duel);
            TaskManager.Instance.Schedule(duel.DuelResultСheckTask, TimeSpan.FromMilliseconds(Delay));
        }
        catch (Exception e)
        {
            // id is missing in the database
            Logger.Warn($"DuelResultСheck: Id={id} not found in duels[], error code: {e}");
            return false;
        }
        return false;
    }

    public DuelDistance DuelDistanceСheck(uint id)
    {
        try
        {
            var duel = _duels[id];
            // Let's check if they ran away from the flag or not
            var currentDistance = MathUtil.CalculateDistance(duel.DuelFlag.Transform.World.Position, duel.Challenger.Transform.World.Position, true);
            if (currentDistance >= DistanceForSurrender)
            {
                // отключаем таймер
                if (duel.DuelDistanceСheckTask == null)
                    return DuelDistance.ChallengerFar; // The one who challenged the other to a duel - that is, who ran away from the flag — is considered to have lost.

                _ = duel.DuelDistanceСheckTask.Cancel();
                duel.DuelDistanceСheckTask = null;
                return DuelDistance.ChallengerFar; // The one who challenged the other to a duel - that is, who ran away from the flag — is considered to have lost.
            }
            // Let's check if they ran away from the flag or not
            currentDistance = MathUtil.CalculateDistance(duel.DuelFlag.Transform.World.Position, duel.Challenged.Transform.World.Position, true);
            if (currentDistance >= DistanceForSurrender)
            {
                // отключаем таймер
                if (duel.DuelDistanceСheckTask == null)
                    return DuelDistance.ChallengedFar; // The one who was challenged to a duel is considered to have surrendered — that is, he fled from the flag.

                _ = duel.DuelDistanceСheckTask.Cancel();
                duel.DuelDistanceСheckTask = null;
                return DuelDistance.ChallengedFar; // The one who was challenged to a duel is considered to have surrendered — that is, he fled from the flag.
            }

            duel.DuelDistanceСheckTask = new DuelDistanceСheckTask(duel);
            TaskManager.Instance.Schedule(duel.DuelDistanceСheckTask, TimeSpan.FromMilliseconds(Delay));
        }
        catch (Exception e)
        {
            // id is missing in the database
            Logger.Warn($"DistanceСheck: Id={id} not found in duels[], error code: {e}");
            return DuelDistance.Error;  // next to the flag
        }
        return DuelDistance.Near;  // next to the flag
    }
}
