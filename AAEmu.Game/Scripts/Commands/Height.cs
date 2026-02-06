using System.Text;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.CryEngine.Objects;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Utils;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class Height : ICommand
{
    public string[] CommandNames { get; set; } = ["height"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "(target)";
    }

    public string GetCommandHelpText()
    {
        return "Gets your or target's current height and that of the supposed floor (using heightmap data)";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var targetPlayer = character;
        if (args.Length > 0)
        {
            targetPlayer = WorldManager.Instance.GetTargetOrSelf(character, args[0], out var firstArg);
        }

        var floorHeight = targetPlayer.ParentWorld.GetHeightUsingHeightMapDat(targetPlayer.Transform.World.Position.X, targetPlayer.Transform.World.Position.Y);
        var defaultCheckHeight = targetPlayer.ParentWorld.GetHeight(targetPlayer.Transform.World.Position);
        var rayCastHeight = targetPlayer.ParentWorld.GetHeightByRayCastOnHeightMapOnly(targetPlayer.Transform.World.Position, targetPlayer.Transform.World.Position.Z);
        CommandManager.SendNormalText(this, messageOutput, $"{targetPlayer.Name} Z-Pos: {character.Transform.World.Position.Z:F1}");
        CommandManager.SendNormalText(this, messageOutput, $"DetectedHeight: {defaultCheckHeight} ({character.Transform.World.Position.Z - defaultCheckHeight:F1})");
        CommandManager.SendNormalText(this, messageOutput, $"HMap Floor: {floorHeight} ({character.Transform.World.Position.Z - floorHeight:F1})");
        CommandManager.SendNormalText(this, messageOutput, $"RayCastHeight: {rayCastHeight} ({character.Transform.World.Position.Z - rayCastHeight:F1})");
        
        // Dump Voxel Data
        /*
        var sb = new StringBuilder();
        var i = 0;
        sb.AppendLine($"Testing at {targetPlayer.Transform.World.Position}");
        foreach (var voxelObject in targetPlayer.ParentWorld.Physics.VoxelObjects)
        {
            i++;
            var voxel = voxelObject.Tag as ObjectDataType6Voxel ;
            if (voxel == null)
            {
                sb.AppendLine($"{i:0000}: {voxelObject.Position.ToVector()} Invalid");
                continue;
            }

            sb.AppendLine($"{i:0000}: {voxelObject.Position.ToVector()} {voxel.BoundingBoxMin} -> {voxel.BoundingBoxMax} | {voxel.VoxelExtraX} {voxel.VoxelExtraY} {voxel.VoxelExtraZ}");
            foreach (var shape in voxelObject.Shapes)
            {
                var hasHitBox = WorldInstance.JBoundingBoxContains2DPoint(shape.WorldBoundingBox, targetPlayer.Transform.World.Position.X, targetPlayer.Transform.World.Position.Y);
                var s = hasHitBox ? " HIT!" : string.Empty;
                var dist = MathF.Abs((targetPlayer.Transform.World.Position.ToJVector() - shape.WorldBoundingBox.Min).Length()); 
                sb.AppendLine($"{i:0000} Shape: Dist: {dist:F1} Box: {shape.WorldBoundingBox}{s}");
            }
            sb.AppendLine();
        }
        File.WriteAllText(@"D:\\Temp\\VoxelExport.txt", sb.ToString());
        */
    }
}
