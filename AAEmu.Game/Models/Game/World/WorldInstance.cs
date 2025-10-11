using System.Collections.Concurrent;
using System.Numerics;
using AAEmu.Commons.IO;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.Indun;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using Jitter2.LinearMath;
using NLog;

namespace AAEmu.Game.Models.Game.World;

/// <summary>
/// Instance of a World
/// </summary>
public class WorldInstance(WorldTemplate template, uint channelId, bool dontFreeInstanceId, uint instanceId)
{
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    // ReSharper disable once InconsistentNaming
    private static Logger Logger = LogManager.GetCurrentClassLogger();

    #region InstanceProperties
    /// <summary>
    /// Keeps track if we need to release the Id or not
    /// </summary>
    private bool IsFixedInstanceId { get; } = dontFreeInstanceId;

    /// <summary>
    /// Instance Id for this world
    /// </summary>
    public uint Id { get; init; } = instanceId;

    /// <summary>
    /// Template of this world
    /// </summary>
    public WorldTemplate Template { get; init; } = template;

    /// <summary>
    /// Channel number for this instance (only for dungeons)
    /// </summary>
    public uint ChannelId { get; init; } = channelId;

    /// <summary>
    /// If this instance is a Dungeon, this links to the dungeon info
    /// </summary>
    public Dungeon DungeonInstance { get; set; }
    #endregion InstanceProperties

    #region GameWorldInstance
    /// <summary>
    /// Collection of Region data
    /// </summary>
    public Region[,] Regions { get; set; }

    /// <summary>
    /// Physics handler
    /// </summary>
    public PhysicsManager Physics { get; private set; }

    /// <summary>
    /// Water definitions
    /// </summary>
    public WaterBodies Water { get; set; }

    /// <summary>
    /// Event handlers
    /// </summary>
    public WorldEvents Events { get; set; } = new();

    /// <summary>
    /// Manager for Quest sphere triggers
    /// </summary>
    public SphereQuestManager SphereQuestManager { get; set; }

    /// <summary>
    /// Manager that handles spawns for this instance
    /// </summary>
    public SpawnManager SpawnManager { get; set; }
    /// <summary>
    /// Manager that handles vehicle spawns for this instance
    /// </summary>
    public SlaveManager SlaveManager { get; set; }

    /// <summary>
    /// Manager that handles pet spawns for this instance
    /// </summary>
    public MateManager MateManager { get; set; }

    /// <summary>
    /// Manager that handles Gimmicks for this instance
    /// </summary>
    public GimmickManager GimmickManager { get; set; }

    /// <summary>
    /// Manager that handles Transfers for this instance 
    /// </summary>
    public TransferManager TransferManager { get; set; }

    /// <summary>
    /// Global Instance flag to check if PvP is allowed here
    /// </summary>
    public bool AllowPvP
    {
        get
        {
            return DungeonInstance?._indunZone?.PvP ?? true;
        }
    }
    #endregion GameWorldInstance

    #region GameObjectLists
    /// <summary>
    /// List of all GameObjects in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, GameObject> _objects = new();

    /// <summary>
    /// List of all BaseUnits in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, BaseUnit> _baseUnits = new();

    /// <summary>
    /// List of all Units in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Unit> _units = new();

    /// <summary>
    /// List of all Doodads in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Doodad> _doodads = new();

    /// <summary>
    /// List of all Npcs in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Npc> _npcs = new();

    /// <summary>
    /// List of all Transfers in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Transfer> _transfers = new();

    /// <summary>
    /// List of all Gimmicks in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Gimmick> _gimmicks = new();

    /// <summary>
    /// List of all Slaves in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Slave> _slaves = new();

    /// <summary>
    /// List of all Mates in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Units.Mate> _mates = new();

    /// <summary>
    /// List of all Players in this instance
    /// </summary>
    private readonly ConcurrentDictionary<uint, Character> _characters = new();
    #endregion GameObjectLists

    ~WorldInstance()
    {
        CleanupInstance();
        if (!IsFixedInstanceId)
            WorldIdManager.Instance.ReleaseId(Id);
        Logger.Info($"WorldInstance {this} removed");
    }

    /// <summary>
    /// Default formatting of World name in logs
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{Id}-{Template.Name}({Template.Id})";
    }

    #region PhysicalProperties
    /// <summary>
    /// Checks if target position is inside a body of water
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public bool IsWater(Vector3 position) => IsWater(position, out _);

    /// <summary>
    /// Checks if target position is inside a body of water and returns it's flow direction (if available)
    /// </summary>
    /// <param name="point"></param>
    /// <param name="flowDirection"></param>
    /// <returns></returns>
    public bool IsWater(Vector3 point, out Vector3 flowDirection)
    {
        if (Water != null)
            return Water.IsWater(point, out flowDirection);

        flowDirection = Vector3.Zero;

        if (point.Z <= Template.OceanLevel)
            return true;

        // TODO: Check shapes
        return false;
    }

    /// <summary>
    /// Line linear interpolation
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="target">value 0 to 1</param>
    /// <returns></returns>
    private static float Lerp(float start, float end, float target)
    {
        return start + (end - start) * target;
    }

    /// <summary>
    /// Square linear interpolation
    /// </summary>
    /// <param name="cX0Y0">Bottom-Left</param>
    /// <param name="cX1Y0">Bottom-Right</param>
    /// <param name="cX0Y1">Top-Left</param>
    /// <param name="cX1Y1">Top-Right</param>
    /// <param name="tx">value 0 to 1</param>
    /// <param name="ty">value 0 to 1</param>
    /// <returns></returns>
    private static float Blerp(float cX0Y0, float cX1Y0, float cX0Y1, float cX1Y1, float tx, float ty)
    {
        return Lerp(Lerp(cX0Y0, cX1Y0, tx), Lerp(cX0Y1, cX1Y1, tx), ty);
    }

    /// <summary>
    /// Picks the nearest 4 points of a square that contain target position
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private static System.Drawing.Rectangle FindNearestSignificantPoints(int x, int y)
    {
        return new System.Drawing.Rectangle(x - x % 2, y - y % 2, 2, 2);
    }

    /// <summary>
    /// Gets height at target position using various methods (recommended way to get solid surface height)
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public float GetHeight(Vector3 pos)
    {
        // refPos to us as a starting point to find surface, we put this 2m higher that requested location
        // to take into account possible model clipping
        var refPos = pos with { Z = pos.Z + 2f };
        // First try using physics engine
        if (Physics is { WorldHeightMapTester: not null })
            return GetHeightByRayCastOnHeightMapOnly(refPos, pos.Z);

        // Fallback to normal heightmap.dat only data
        var target = GetHeightUsingHeightMapDat(refPos.X, refPos.Y);
        if (target > 0f)
            return target;

        // Last resort using .bai files mesh
        return Template.GeoData?.GetHeight(refPos, pos.Z) ?? 0f;
    }

    public float GetReferenceHeight(NpcAi ai, Vector3 pos, uint zoneId)
    {
        float finalHeight;

        // 0. Just in case.
        if (ai == null)
        {
            finalHeight = GetHeight(pos);
            return finalHeight;
        }

        // 1. If an NPC can fly, the height is taken from the spawner's position.
        if (ai.Owner.CanFly)
        {
            finalHeight = ai.Owner.Spawner.Position.Z;
            return finalHeight;
        }

        // 2. For HoldPositionBehavior and IdleBehavior, the height is taken from the spawner.
        switch (ai.GetCurrentBehavior())
        {
            case HoldPositionBehavior:
            case IdleBehavior:
                finalHeight = ai.Owner.Spawner.Position.Z;
                return finalHeight;
        }

        // 3. Terrain height retrieval
        finalHeight = GetHeight(pos);
        if (finalHeight != 0/* && Math.Abs(worldHeight - Spawner.Position.Z) <= 0.1f*/)
        {
            return finalHeight;
        }

        // 4. Take the default height
        return ai.Owner.Spawner?.Position.Z ?? ai.Owner.Transform.World.Position.Z;
    }

    /// <summary>
    /// Gets height at target position using interpolation from heightmap.dat data
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public float GetHeightUsingHeightMapDat(float x, float y)
    {
        // return GetRawHeightMapHeight((int)x, (int)y); // <-- the old way we used to do things

        // Get bordering points
        var border = FindNearestSignificantPoints((int)Math.Floor(x), (int)Math.Floor(y));

        // Get heights for these points
        var heightTl = Template.GetHeightMapHeight(border.Left, border.Top);
        var heightTr = Template.GetHeightMapHeight(border.Right, border.Top);
        var heightBl = Template.GetHeightMapHeight(border.Left, border.Bottom);
        var heightBr = Template.GetHeightMapHeight(border.Right, border.Bottom);
        var offX = (x - border.Left) / 2;
        var offY = (y - border.Top) / 2;
        var height = Blerp(heightTl, heightTr, heightBl, heightBr, offX, offY); // bilinear interpolation

        return height;
    }

    /// <summary>
    /// Get Sector at specific offset
    /// </summary>
    /// <param name="sectorX">X offset of the Sector</param>
    /// <param name="sectorY">Y offset of the Sector</param>
    /// <returns></returns>
    public Region GetRegion(int sectorX, int sectorY)
    {
        if (Template.ValidRegion(sectorX, sectorY))
            if (Regions[sectorX, sectorY] == null)
                return Regions[sectorX, sectorY] = new Region(this, sectorX, sectorY, 0);
            else
                return Regions[sectorX, sectorY];

        return null;
    }

    /// <summary>
    /// Gets a sector at a specific world position
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public Region GetRegionByPos(Vector3 pos)
    {
        var sectorX = (int)(pos.X / WorldManager.REGION_SIZE);
        var sectorY = (int)(pos.Y / WorldManager.REGION_SIZE);
        if (Template.ValidRegion(sectorX, sectorY))
            if (Regions[sectorX, sectorY] == null)
                return Regions[sectorX, sectorY] = new Region(this, sectorX, sectorY, 0);
            else
                return Regions[sectorX, sectorY];

        return null;
    }

    /// <summary>
    /// Gets all T GameObjects within a given Cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public List<T> GetInCell<T>(int x, int y) where T : class
    {
        var result = new List<T>();
        var regions = new List<Region>();
        for (var a = x * WorldManager.SECTORS_PER_CELL; a < (x + 1) * WorldManager.SECTORS_PER_CELL; a++)
        {
            for (var b = y * WorldManager.SECTORS_PER_CELL; b < (y + 1) * WorldManager.SECTORS_PER_CELL; b++)
            {
                if (Template.ValidRegion(a, b) && Regions[a, b] != null)
                    regions.Add(Regions[a, b]);
            }
        }

        foreach (var region in regions)
            region.GetList(result, 0);
        return result;
    }

    /// <summary>
    /// Creates and starts the physics engine for this world instance
    /// </summary>
    public void StartPhysics()
    {
        Logger.Debug($"Starting physics engine for instance {this}");
        Physics = new PhysicsManager { SimulationWorld = this };
        Physics.Initialize();
        Physics.InitializeTerrain();
        Physics.StartPhysics();
    }

    /// <summary>
    /// Loads water body date for this world
    /// </summary>
    public void LoadWaterBodies()
    {
        // Try to load from saved json data
        var customFile = Path.Combine(FileManager.AppPath, "Data", "Worlds", Template.Name, "water_bodies.json");
        if (!File.Exists(customFile))
        {
            return;
        }

        Logger.Debug($"Loading water body data for instance {this}");
        if (WaterBodies.Load(customFile, out var newWater))
        {
            Water = newWater;
        }
    }
    #endregion PhysicalProperties
    
    #region GetGameObjects
    /// <summary>
    /// Get GameObject by its ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public GameObject GetGameObject(uint objId)
    {
        return _objects.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Get Unit by its ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public BaseUnit GetBaseUnit(uint objId)
    {
        return _baseUnits.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Get Doodad by its ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public Doodad GetDoodad(uint objId)
    {
        return _doodads.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Get Doodad by its database Id
    /// </summary>
    /// <param name="dbId"></param>
    /// <returns></returns>
    public Doodad GetDoodadByDbId(uint dbId)
    {
        var ret = _doodads.FirstOrDefault(x => x.Value.DbId == dbId).Value;
        return ret;
    }

    /// <summary>
    /// Get House by its database Id
    /// </summary>
    /// <param name="houseDbId"></param>
    /// <returns></returns>
    public List<Doodad> GetDoodadByHouseDbId(uint houseDbId)
    {
        var ret = _doodads.Where(x => x.Value.OwnerDbId == houseDbId).Select(y => y.Value).ToList();
        return ret;
    }

    /// <summary>
    /// Get Active Unit by ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public Unit GetUnit(uint objId)
    {
        return _units.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Get active NPC by ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public Npc GetNpc(uint objId)
    {
        return _npcs.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Gets the first active NPC with a specific TemplateId
    /// </summary>
    /// <param name="templateId"></param>
    /// <returns></returns>
    public Npc GetNpcByTemplateId(uint templateId)
    {
        return _npcs.Values.FirstOrDefault(x => x.TemplateId == templateId);
    }

    /// <summary>
    /// Manually assign a Npc to the npc objects list (used for tests only) 
    /// </summary>
    /// <param name="objId"></param>
    /// <param name="npc"></param>
    internal void SetNpc(uint objId, Npc npc)
    {
        _npcs[objId] = npc;
    }

    /// <summary>
    /// Gets a list of all player characters in this instance
    /// </summary>
    /// <returns></returns>
    public List<Character> GetAllCharacters()
    {
        return _characters.Values.ToList();
    }

    /// <summary>
    /// Gets a character in this instance by their ObjId 
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public Character GetCharacterByObjId(uint objId)
    {
        return _characters.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Checks if target player is in this instance
    /// </summary>
    /// <param name="playerId"></param>
    /// <returns></returns>
    public bool HasCharacter(uint playerId)
    {
        return _characters.Values.Any(x => x.Id == playerId);
    }

    /// <summary>
    /// Get the number of characters in the current instance
    /// </summary>
    /// <returns></returns>
    public int GetCharacterCount()
    {
        return _characters.Count;
    }

    /// <summary>
    /// Returns a contacted string of player names in this instance
    /// </summary>
    /// <param name="maxPlayerNames">Maximum number of names to show, when there are more, returns a number instead</param>
    /// <returns></returns>
    public string ListPlayerNames(uint maxPlayerNames)
    {
        if (_characters.Count > maxPlayerNames)
        {
            return _characters.Count.ToString();
        }

        if (_characters.Count <= 0)
        {
            return "[none]";
        }

        var res = string.Empty;
        foreach (var player in _characters.Values)
        {
            if (!string.IsNullOrWhiteSpace(res))
                res += ", " + player.Name;
            else
                res += player.Name;
        }
        return res;
    }

    /// <summary>
    /// Adds a GameObject to the list of existing objects on the server
    /// </summary>
    /// <param name="obj"></param>
    public void AddObject(GameObject obj)
    {
        if (obj == null)
            return;

        _objects.TryAdd(obj.ObjId, obj);

        if (obj is BaseUnit baseUnit)
            _baseUnits.TryAdd(baseUnit.ObjId, baseUnit);
        if (obj is Unit unit)
            _units.TryAdd(unit.ObjId, unit);
        if (obj is Doodad doodad)
            _doodads.TryAdd(doodad.ObjId, doodad);
        if (obj is Npc npc)
            _npcs.TryAdd(npc.ObjId, npc);
        if (obj is Character character)
        {
            // Add to server, should already be added at this point, but add it again anyway
            WorldManager.Instance.TryAddCharacter(character);
            // Add to instance
            _characters.TryAdd(character.ObjId, character);
        }
        if (obj is Transfer transfer)
            _transfers.TryAdd(transfer.ObjId, transfer);
        if (obj is Gimmick gimmick)
            _gimmicks.TryAdd(gimmick.ObjId, gimmick);
        if (obj is Slave slave)
            _slaves.TryAdd(slave.ObjId, slave);
        if (obj is Units.Mate mate)
            _mates.TryAdd(mate.ObjId, mate);
    }

    /// <summary>
    /// Removes a GameObject from the list of "existing" objects on the server
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public bool RemoveObject(GameObject obj)
    {
        if (obj == null)
            return true;

        var res = false;

        res |= _objects.TryRemove(obj.ObjId, out _);
        if (obj is BaseUnit)
            res |= _baseUnits.TryRemove(obj.ObjId, out _);
        if (obj is Unit)
            res |= _units.TryRemove(obj.ObjId, out _);
        if (obj is Doodad)
            res |= _doodads.TryRemove(obj.ObjId, out _);
        if (obj is Npc)
            res |= _npcs.TryRemove(obj.ObjId, out _);
        if (obj is Character)
        {
            // Server
            // WorldManager.Instance.TryRemoveCharacter(obj.ObjId);
            // Instance
            res |= _characters.TryRemove(obj.ObjId, out _);
        }
        if (obj is Transfer)
            res |= _transfers.TryRemove(obj.ObjId, out _);
        if (obj is Gimmick)
            res |= _gimmicks.TryRemove(obj.ObjId, out _);
        if (obj is Slave)
            res |= _slaves.TryRemove(obj.ObjId, out _);
        if (obj is Units.Mate mate)
            res |= _mates.TryRemove(mate.ObjId, out _);

        return res;
    }

    /// <summary>
    /// Gets list of all NPCs in this instance
    /// </summary>
    /// <returns></returns>
    public List<Npc> GetAllNpcs()
    {
        return _npcs.Values.ToList();
    }

    /// <summary>
    /// Gets a list of all vehicles in this instance
    /// </summary>
    /// <returns></returns>
    public List<Slave> GetAllSlaves()
    {
        return _slaves.Values.ToList();
    }

    /// <summary>
    /// Gets a list of all pets in this instance
    /// </summary>
    /// <returns></returns>
    public List<Units.Mate> GetAllMates()
    {
        return _mates.Values.ToList();
    }

    /// <summary>
    /// Gets a list of all doodads in this instance
    /// </summary>
    /// <returns></returns>
    public List<Doodad> GetAllDoodads()
    {
        return _doodads.Values.ToList();
    }

    /// <summary>
    /// Gets a list of all gimmicks in this instance
    /// </summary>
    /// <returns></returns>
    public List<Gimmick> GetAllGimmicks()
    {
        return _gimmicks.Values.ToList();
    }

    /// <summary>
    /// Get a list of NPCs that have loot and are past the "make public" time
    /// </summary>
    /// <returns></returns>
    public HashSet<Npc> GetNpcsToMakePublicLooting()
    {
        HashSet<Npc> temp;
        lock (_npcs)
        {
            temp = [.. _npcs.Values];
        }

        var res = new HashSet<Npc>();
        foreach (var item in temp.Where(item => item.LootingContainer.CanMakePublic()))
            res.Add(item);
        return res;
    }
    #endregion GetGameObjects

    #region events

    public void CleanupInstance()
    {
        // Stop respawn system (check for null as SpawnManager may not be initialized in tests)
        if (SpawnManager == null)
            return;

        SpawnManager.Stop(); // Stop respawn loop
        try
        {
            SpawnManager.DeleteAllSpawners(); // Remove spawners and their children
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
        try
        {
            _ = SpawnManager.DeSpawnAll(); // Delete whatever is remaining
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
        Logger.Debug($"Removed objects from WorldInstance {this}");
    }
    
    /// <summary>
    /// Handle "is still in combat" related things
    /// </summary>
    /// <param name="unit"></param>
    private static void CombatTick(Unit unit)
    {
        // TODO: Make it so you can also become out of combat if you are not on any aggro lists
        if (unit.IsInBattle && unit.LastCombatActivity.AddSeconds(WorldManager.DefaultCombatTimeout) < DateTime.UtcNow)
        {
            unit.IsInBattle = false;
        }

        if (unit is Character { IsInPostCast: true } character && character.LastCast.AddSeconds(5) < DateTime.UtcNow)
        {
            character.IsInPostCast = false;
        }
    }
    
    #endregion events

    /// <summary>
    /// Gets a solid floor location using ray-casting on the heightmaptester
    /// </summary>
    /// <param name="targetPosition"></param>
    /// <returns></returns>
    public float GetHeightByRayCastOnHeightMapOnly(Vector3 targetPosition, float defaultHeight)
    {
        var totalHeight = 0f;
        var validCount = 0;
        // var testCount = 0;

        // If not initialized, return the reference point's height
        if (Physics == null || Physics.PhysWorld == null)
            return defaultHeight;
        
        // Initially start from slightly above the reference point casting downwards, this should technically be the model's height + 1 or so 
        var mainRayStart = new JVector(targetPosition.X, targetPosition.Z + 3f, targetPosition.Y);
        if (Physics.WorldHeightMapTester.RayCast(mainRayStart, -JVector.UnitY, out var _, out var lambda))
        {
            // testCount++;
            // Logger.Debug($"Total ray checks: {testCount}");
            return mainRayStart.Y - lambda;
        }

        // If not a direct hit, scan the 5 x 5 cm area around the intended position and try to get its average
        // If position is exactly between the two triangles, it will not hit anything. This is a workaround for this situation
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
        {
            // testCount++;
            var v = new JVector(mainRayStart.X + (x * 0.05f), mainRayStart.Y, mainRayStart.Z + (y * 0.05f));
            if (Physics.WorldHeightMapTester.RayCast(v, -JVector.UnitY, out _, out var nearPointDistance))
            {
                totalHeight += mainRayStart.Y - nearPointDistance;
                validCount++;
            }
        }

        // If there are still no hits, we might be under any floor, and we need to do a more dramatic raycast
        if (validCount <= 0)
        {
            // testCount++;
            mainRayStart = new JVector(targetPosition.X, 5000f, targetPosition.Y);
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
            {
                var v = new JVector(mainRayStart.X + (x * 0.05f), 5000f, mainRayStart.Z + (y * 0.05f));
                if (Physics.WorldHeightMapTester.RayCast(v, -JVector.UnitY, out var _, out var nearMaxPointDistance))
                {
                    totalHeight += 5000f - nearMaxPointDistance;
                    validCount++;
                }
            }
        }

        // Logger.Debug($"Total ray checks: {testCount} ({validCount} hits)");
        return validCount > 0 ? totalHeight / validCount : 0f;
    }
}
