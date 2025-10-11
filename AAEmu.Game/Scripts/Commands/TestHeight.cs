using System.Diagnostics;
using System.Numerics;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Utils;
using AAEmu.Game.Utils.Scripts;
using Jitter2.Collision;
using Jitter2.LinearMath;

namespace AAEmu.Game.Scripts.Commands;

public enum TestHeightMode : byte
{
    HeightMap,
    BaiData,
    RayCast
}

public class TestHeight : ICommand
{
    public string[] CommandNames { get; set; } = ["testheightvisualizer", "test_height_visualizer", "thv"];
    private const float TargetX = 22500f;
    private const float TargetY = 18500f;
    private const float TargetZ = 10f;
    private static List<Doodad> PlacedMarkers { get; } = [];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "(target) [testpos||mark||line||clear] [geo||ray]";
    }

    public string GetCommandHelpText()
    {
        return "Gets your or target's current height and that of the supposed floor (using heightmap data)\n" +
               "testpos will move you near Freedich underwater\r" +
               "mark creates a grid of pillar doodads used for measuring the floor at 2m intervals (exact heightmap points)\r" +
               "line creates a cross of pillar doodads used for measuring the floor at 1m intervals (for in-between points)\r" +
               "Adding a GEO argument at the end will use data from the bail files instead of the heightmap.dat file.";
    }

    private float ScriptGetHeight(Vector3 pos, WorldInstance world, TestHeightMode mode)
    {
        switch (mode)
        {
            case TestHeightMode.HeightMap:
                return world.GetHeightUsingHeightMapDat(pos.X, pos.Y);
            case TestHeightMode.BaiData:
                return world.Template.GeoData.GetHeight(pos, pos.Z);
            case TestHeightMode.RayCast:
                return world.GetHeightByRayCastOnHeightMapOnly(pos, pos.Z);
                /*
                var ceiling = 10000f;
                var rayStart = pos.ToJVector() with { Y = ceiling };
                if (world.Physics.PhysWorld.DynamicTree.RayCast(rayStart, -JVector.UnitY, ceiling, null, null,
                        out var proxy, out var normal, out var lambda))
                    return ceiling - lambda;
                else
                    return 0f;//pos.Z;
                */
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var targetPlayer = character;
        var firstarg = 0;
        if (args.Length > 0)
        {
            targetPlayer = WorldManager.Instance.GetTargetOrSelf(character, args[0], out firstarg);
        }

        var heightMode = TestHeightMode.HeightMap;
        foreach (var s in args)
        {
            if (s.ToLower() == "map")
                heightMode = TestHeightMode.HeightMap;
            if (s.ToLower() == "geo")
                heightMode = TestHeightMode.BaiData;
            if (s.ToLower() == "ray")
                heightMode = TestHeightMode.RayCast;
        }

        if (args.Length > firstarg && args[firstarg].ToLower() == "clear")
        {
            CommandManager.SendNormalText(this, messageOutput, $"Clearing |cFFFFFFFF{PlacedMarkers.Count}|r markers");
            foreach (var doodad in PlacedMarkers.ToArray())
            {
                try
                {
                    doodad.Spawner.Id = 0xffffffff; // removed from the game manually
                    doodad.Hide();
                }
                catch
                {
                    // Ignore
                }
            }
            PlacedMarkers.Clear();
        }
        else if (args.Length > firstarg && args[firstarg].ToLower() == "testpos")
        {
            targetPlayer.DisabledSetPosition = true;
            targetPlayer.SendPacket(new SCTeleportUnitPacket(0, 0, TargetX, TargetY, TargetZ, 0f));
            targetPlayer.SendMessage($"[Move] |cFFFFFFFF{targetPlayer.Name}|r moved to X: {TargetX}, Y: {TargetY}, Z: {TargetZ}");
        }
        else if (args.Length > firstarg && args[firstarg].ToLower() == "mark")
        {
            // Place markers
            var rX = (int)Math.Floor(targetPlayer.Transform.World.Position.X);
            rX -= rX % 2;
            var rY = (int)Math.Floor(targetPlayer.Transform.World.Position.Y);
            rY -= rY % 2;
            uint unitId = 5622; // Pillar
            for (var y = rY - 10; y <= rY + 10; y += 2)
            for (var x = rX - 10; x <= rX + 10; x += 2)
            {
                if (!DoodadManager.Instance.Exist(unitId))
                {
                    return;
                }

                var doodadSpawner = new DoodadSpawner();
                doodadSpawner.ParentWorld = character.ParentWorld;
                doodadSpawner.Id = 0;
                doodadSpawner.UnitId = unitId;
                doodadSpawner.Position = character.Transform.CloneAsSpawnPosition();
                doodadSpawner.Position.X = x;
                doodadSpawner.Position.Y = y;
                doodadSpawner.Position.Z = ScriptGetHeight(doodadSpawner.Position.AsPositionVector(), doodadSpawner.ParentWorld, heightMode);
                doodadSpawner.Position.Yaw = 0;
                doodadSpawner.Position.Pitch = 0;
                doodadSpawner.Position.Roll = 0;
                PlacedMarkers.Add(doodadSpawner.Spawn(0));
            }
        }
        else if (args.Length > firstarg && args[firstarg].ToLower() == "line")
        {
            // Place markers
            var rXX = (int)Math.Floor(targetPlayer.Transform.World.Position.X);
            rXX = rXX - rXX % 2;
            var rYY = (int)Math.Floor(targetPlayer.Transform.World.Position.Y);
            rYY = rYY - rYY % 2;

            float rX = rXX;
            float rY = rYY;
            uint unitId = 5622; // Pillar
            for (var x = rX - 10f; x <= rX + 10f; x += 1f)
            {
                if (!DoodadManager.Instance.Exist(unitId))
                {
                    return;
                }

                var doodadSpawner = new DoodadSpawner();
                doodadSpawner.ParentWorld = character.ParentWorld;
                doodadSpawner.Id = 0;
                doodadSpawner.UnitId = unitId;
                doodadSpawner.Position = character.Transform.CloneAsSpawnPosition();
                doodadSpawner.Position.X = x + 0.01f;
                doodadSpawner.Position.Y = rY + 0.01f;
                doodadSpawner.Position.Z = ScriptGetHeight(doodadSpawner.Position.AsPositionVector(), doodadSpawner.ParentWorld, heightMode);
                doodadSpawner.Position.Yaw = 0;
                doodadSpawner.Position.Pitch = 0;
                doodadSpawner.Position.Roll = 0;
                PlacedMarkers.Add(doodadSpawner.Spawn(0));
            }

            for (var y = rY - 10f; y <= rY + 10f; y += 1f)
            {
                if (!DoodadManager.Instance.Exist(unitId))
                {
                    return;
                }

                var doodadSpawner = new DoodadSpawner
                {
                    ParentWorld = character.ParentWorld, Id = 0, UnitId = unitId, Position = character.Transform.CloneAsSpawnPosition()
                };
                doodadSpawner.Position.X = rX;
                doodadSpawner.Position.Y = y;
                doodadSpawner.Position.Z = ScriptGetHeight(doodadSpawner.Position.AsPositionVector(), doodadSpawner.ParentWorld, heightMode);
                doodadSpawner.Position.Yaw = 0;
                doodadSpawner.Position.Pitch = 0;
                doodadSpawner.Position.Roll = 0;
                PlacedMarkers.Add(doodadSpawner.Spawn(0));
            }
        }
        else if (args.Length > firstarg && args[firstarg].ToLower() == "benchmark")
        {
            // Place markers
            var rX = (int)Math.Floor(character.Transform.World.Position.X);
            rX -= rX % 2;
            var rY = (int)Math.Floor(character.Transform.World.Position.Y);
            rY -= rY % 2;
            var sw = new Stopwatch();
            sw.Start();
            var totalHeight = 0f;
            for (var n = 0; n < 1000; n++)
            for (var y = rY - 100; y <= rY + 100; y += 2)
            for (var x = rX - 100; x <= rX + 100; x += 2)
            {
                var testPos = character.Transform.World.Position + new Vector3(x, y, 0);
                var h = ScriptGetHeight(testPos, character.ParentWorld, heightMode);
                totalHeight += h;
            }
            sw.Stop();
            CommandManager.SendNormalText(this, messageOutput, $"Benchmark 1000 x 100x100m area test, time: {sw.ElapsedMilliseconds}ms, mode: {heightMode}, th: {totalHeight:F1}");
        }
        else
        {
            // Show info
            var world = WorldManager.Instance.GetWorldTemplateByZoneKey(targetPlayer.Transform.ZoneId);

            var height = ScriptGetHeight(targetPlayer.Transform.World.Position, targetPlayer.ParentWorld, heightMode);
            // var height = world.GetHeight(targetPlayer.Transform.World.Position.X, targetPlayer.Transform.World.Position.Y);
            var hDelta = character.Transform.World.Position.Z - height;
            CommandManager.SendNormalText(this, messageOutput, $"{targetPlayer.Name} Z-Pos: {character.Transform.World.Position.Z} - Floor: {height} (mode:{heightMode}) - HeightmapDelta: {hDelta}");
            CommandManager.SendNormalText(this, messageOutput, $"|cFFFFFFFF{targetPlayer.Name}|r X: |cFFFFFFFF{targetPlayer.Transform.World.Position.X:F1}|r  Y: |cFFFFFFFF{targetPlayer.Transform.World.Position.Y:F1}|r  Z: |cFFFFFFFF{targetPlayer.Transform.World.Position.Z:F1}|r ");

            var borderLeft = (int)Math.Floor(targetPlayer.Transform.World.Position.X);
            borderLeft = borderLeft - borderLeft % 2;
            var borderRight =
                borderLeft +
                2; // we're using a divider of 2 of the heightmaps in memory, so we need to compensate with that in mind (instead of 1)
            var borderBottom = (int)Math.Floor(targetPlayer.Transform.World.Position.Y);
            borderBottom -= borderBottom % 2;
            var borderTop = borderBottom + 2;

            // Get heights for these points
            var heightTL = world.GetHeightMapHeight(borderLeft, borderTop);
            var heightTR = world.GetHeightMapHeight(borderRight, borderTop);
            var heightBL = world.GetHeightMapHeight(borderLeft, borderBottom);
            var heightBR = world.GetHeightMapHeight(borderRight, borderBottom);
            var matTL = world.GetHeightMapMaterial(borderLeft, borderTop);
            var matTR = world.GetHeightMapMaterial(borderRight, borderTop);
            var matBL = world.GetHeightMapMaterial(borderLeft, borderBottom);
            var matBR = world.GetHeightMapMaterial(borderRight, borderBottom);
            CommandManager.SendNormalText(this, messageOutput, $"TL @ {borderLeft}x{borderTop} = {heightTL} (m{matTL})");
            CommandManager.SendNormalText(this, messageOutput, $"TR @ {borderRight}x{borderTop} = {heightTR} (m{matTR})");
            CommandManager.SendNormalText(this, messageOutput, $"BL @ {borderLeft}x{borderBottom} = {heightBL} (m{matBL})");
            CommandManager.SendNormalText(this, messageOutput, $"BR @ {borderRight}x{borderBottom} = {heightBR} (m{matBR})");
        }
    }
}
