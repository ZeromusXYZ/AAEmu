using System.Diagnostics;
using System.Numerics;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Utils;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class TestNavMesh : ICommand
{
    public string[] CommandNames { get; set; } = ["testnavmesh", "test_navmesh"];
    public static List<BaseUnit> Markers { get; set; } = [];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "";
    }

    public string GetCommandHelpText()
    {
        return "Shows route to target";
    }

    private static void ClearMarkers()
    {
        foreach (var marker in Markers)
        {
            ObjectIdManager.Instance.ReleaseId(marker.ObjId);
            marker.Delete();
        }
        Markers.Clear();
    }

    private static void AddDoodadMarker(WorldInstance world, Vector3 pos, uint doodadTemplateId)
    {
        var markerDoodad = DoodadManager.Instance.Create(world, 0, doodadTemplateId);
        markerDoodad.Transform.Local.SetPosition(pos);
        markerDoodad.Show();
        Markers.Add(markerDoodad);
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var stonePostDoodad = 5622u; // Stone Post
        var crescentThroneFlagDoodad = 4763u; // Crescent Throne Flag 
        
        ClearMarkers();
        if (character.CurrentTarget is not Npc npc)
        {
            var closestNodePos = character.ParentWorld.Template.GeoData.FindСlosestToTheCurrent(character.Transform.ZoneId, character.Transform.World.Position, 0);
            messageOutput.SendMessage($"Your closest node is: {closestNodePos}");
            AddDoodadMarker(character.ParentWorld, closestNodePos.Pos, crescentThroneFlagDoodad);
            return;
        }
        var world = character.ParentWorld;
        var pos = world.Template.GeoData.FindСlosestToTheCurrent(npc.Transform.ZoneId, npc.Transform.World.Position, 0);
        messageOutput.SendMessage($"Closest to {npc.Transform.World.Position} -> {pos}");
        var watch = new Stopwatch();
        watch.Start();
        var foundPath = npc.FindPath(character).ToList();
        // var foundPath = npc.Ai.PathNode.FindPath(npc.ParentWorld, npc.Transform.World.Position, character.Transform.World.Position, out var hasDifferentNodeTypes).ToList();
        watch.Stop();
        messageOutput.SendMessage($"FindPath Took {watch.ElapsedMilliseconds}ms");
        foundPath.Insert(0, npc.Transform.World.Position);
        //foundPath.Add(character.Transform.World.Position);
        //npc.Ai.PathNode.FoundPath = foundPath;
        var lastPos = npc.Transform.World.Position;
        foreach (var v3 in foundPath)
        {
            var d = (lastPos - v3).Length();
            messageOutput.SendMessage($"-> {v3} (d {d:F1}, r {(v3 - character.Transform.World.Position).Length():F1}, a {MathUtil.CalculateAngleFrom(lastPos,v3):F1}°)");
            lastPos = v3;
            AddDoodadMarker(world, v3, stonePostDoodad);
        }
        messageOutput.SendMessage($"Reduced:");
        // messageOutput.SendMessage($"Reduced (multi-type {hasDifferentNodeTypes}):");
        // var reducedPath = hasDifferentNodeTypes ? foundPath : world.Template.GeoData.ReducePath(foundPath.ToList(), 5).ToList();
        var reducedPath = world.Template.GeoData.ReducePath(foundPath.ToList(), 5).ToList();
        //reducedPath.Insert(0, npc.Transform.World.Position);
        //reducedPath.Add(character.Transform.World.Position);
        lastPos = npc.Transform.World.Position;
        foreach (var v3 in reducedPath)
        {
            var d = (lastPos - v3).Length();
            messageOutput.SendMessage($"=> {v3} (d {d:F1}, r {(v3 - character.Transform.World.Position).Length():F1}, a {MathUtil.CalculateAngleFrom(lastPos,v3):F1}°)");
            lastPos = v3;
            AddDoodadMarker(world, v3, crescentThroneFlagDoodad);
        }
    }
}
