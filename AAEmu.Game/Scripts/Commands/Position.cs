using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Scripts.Commands
{
    public class Position : ICommand
    {
        public void OnLoad()
        {
            string[] names = { "position", "pos" };
            CommandManager.Instance.Register(names, this);
        }

        public string GetCommandLineHelp()
        {
            return "(player)";
        }

        public string GetCommandHelpText()
        {
            return "Displays information about the position of you, or your target if a target is selected or provided as a argument.";
        }

        public void Execute(Character character, string[] args)
        {

            AAEmu.Game.Models.Game.Units.BaseUnit targetPlayer = null;
            if (args.Length > 0)
            {
                if (uint.TryParse(args[0], out var targetObjId))
                {
                    var baseUnit = WorldManager.Instance.GetBaseUnit(targetObjId);
                    if (baseUnit != null)
                    {
                        targetPlayer = baseUnit;
                    }
                    else
                    {
                        character.SendMessage("[Position] ObjId: {0} is not a BaseUnit", targetObjId);
                    }
                }
                else
                {
                    targetPlayer = WorldManager.Instance.GetTargetOrSelf(character, args[0], out var firstarg);
                }
            }

            if ((targetPlayer == null) && (character.CurrentTarget != null) && (character.CurrentTarget != character))
            {
                targetPlayer = character.CurrentTarget;
            }

            if (targetPlayer == null)
                targetPlayer = character;

            var pos = targetPlayer.Position;

            var zonename = "???";
            var zone = ZoneManager.Instance.GetZoneByKey(pos.ZoneId);
            if (zone != null)
                zonename = "@ZONE_NAME(" + zone.Id.ToString() + ")";

            character.SendMessage(
                "[Position] |cFFFFFFFF{0}|r (ObjId:{7}) X: |cFFFFFFFF{1:F1}|r  Y: |cFFFFFFFF{2:F1}|r  Z: |cFFFFFFFF{3:F1}|r  RotZ: |cFFFFFFFF{4:F0}|r  ZoneId: |cFFFFFFFF{5}|r {6}",
                targetPlayer.Name, pos.X, pos.Y, pos.Z, pos.RotationZ, pos.ZoneId, zonename, targetPlayer.ObjId);
        }
    }
}
