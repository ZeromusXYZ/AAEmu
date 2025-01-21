using System;
using System.Collections.Generic;
using System.Numerics;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Units.Route;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Utils;
using NLog;

namespace AAEmu.Game.Models.Game.NPChar;

public class NpcSpawnerNpc : Spawner<Npc>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public uint NpcSpawnerTemplateId { get; set; } // spawner template id
    public uint MemberId { get; set; } // npc template id
    public string MemberType { get; set; } // 'Npc'
    public float Weight { get; set; }

    public NpcSpawnerNpc()
    {
    }

    /// <summary>
    /// Creates a new instance of NpcSpawnerNpcs with a Spawner template id (npc_spanwers)
    /// </summary>
    /// <param name="spawnerTemplateId"></param>
    public NpcSpawnerNpc(uint spawnerTemplateId)
    {
        NpcSpawnerTemplateId = spawnerTemplateId;
    }

    public NpcSpawnerNpc(uint spawnerTemplateId, uint memberId)
    {
        NpcSpawnerTemplateId = spawnerTemplateId;
        MemberId = memberId;
        MemberType = "Npc";
    }

    public List<Npc> Spawn(NpcSpawner npcSpawner)
    {
        switch (MemberType)
        {
            case "Npc":
                return SpawnNpc(npcSpawner);
            case "NpcGroup":
                return SpawnNpcGroup(npcSpawner);
            default:
                throw new InvalidOperationException($"Tried spawning an unsupported line from NpcSpawnerNpc - Id: {Id}");
        }
    }

    private List<Npc> SpawnNpc(NpcSpawner npcSpawner)
    {
        var npcs = new List<Npc>();
        var npc = NpcManager.Instance.Create(0, MemberId);
        if (npc == null)
        {
            Logger.Warn($"Npc {MemberId}, from spawner Id {npcSpawner.Id} not exist at db. Spawner Position: {npcSpawner.Position}");
            return null;
        }

        npc.RegisterNpcEvents();

        Logger.Trace($"Spawn npc templateId {MemberId} objId {npc.ObjId} from spawnerId {NpcSpawnerTemplateId} at Position: {npcSpawner.Position}");

        var targetSpawnLocation = npcSpawner.Position.AsPositionVector();

        // Try to pick a random location around
        if (npcSpawner.Template.TestRadiusNpc > 0 && npcSpawner.Template.MaxPopulation > 1)
        {
            var maxRandomRange = 1f * npcSpawner.Template.TestRadiusNpc * npcSpawner.Template.MaxPopulation;
            var randomAngle = ((float)Random.Shared.Next(0,360)).DegToRad();
            var variantX = (float)Math.Sin(randomAngle) * maxRandomRange;
            var variantY = (float)Math.Cos(randomAngle) * maxRandomRange;
            var newTargetSpawnLocation = targetSpawnLocation + new Vector3(variantX, variantY, 0f);
            // TODO: Check if this location isn't in a "wall"
            targetSpawnLocation = newTargetSpawnLocation;
        }

        if (!npc.CanFly)
        {
            var newZ = WorldManager.Instance.GetHeight(npcSpawner.Position.ZoneId, targetSpawnLocation.X, targetSpawnLocation.Y); // Убираем await
            if (Math.Abs(targetSpawnLocation.Z - newZ) < 1f)
            {
                targetSpawnLocation.Z = newZ;
            }
        }

        npc.Transform.ApplyWorldSpawnPosition(npcSpawner.Position);
        npc.Transform.Local.Position = targetSpawnLocation;
        if (npc.Transform == null)
        {
            Logger.Error($"Can't spawn npc {MemberId} from spawnerId {NpcSpawnerTemplateId}. Transform is null.");
            return null;
        }

        npc.Transform.InstanceId = npc.Transform.WorldId;
        npc.InstanceId = npc.Transform.WorldId;

        if (npc.Ai != null)
        {
            npc.Ai.HomePosition = npc.Transform.World.Position;
            npc.Ai.IdlePosition = npc.Ai.HomePosition;
            npc.Ai.GoToSpawn();
        }

        npc.Spawner = npcSpawner;
        npc.Spawner.RespawnTime = (int)Rand.Next(npc.Spawner.Template.SpawnDelayMin, npc.Spawner.Template.SpawnDelayMax);
        npc.Spawn();

        var world = WorldManager.Instance.GetWorld(npc.Transform.WorldId);
        world.Events.OnUnitSpawn(world, new OnUnitSpawnArgs { Npc = npc });
        npc.Simulation = new Simulation(npc);

        if (npc.Ai != null && !string.IsNullOrWhiteSpace(npcSpawner.FollowPath))
        {
            if (!npc.Ai.LoadAiPathPoints(npcSpawner.FollowPath, false))
                Logger.Warn($"Failed to load {npcSpawner.FollowPath} for NPC {npc.TemplateId} ({npc.ObjId})");
        }

        npcs.Add(npc);
        return npcs;
    }

    private List<Npc> SpawnNpcGroup(NpcSpawner npcSpawner)
    {
        return SpawnNpc(npcSpawner);
    }
}
