using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Templates;

public class DynamicBonusTemplate
{
    public UnitAttribute Attribute { get; set; }
    public UnitModifierType ModifierType { get; set; }
    public uint FuncId { get; set; }
    public string FuncType { get; set; }

    public double GetValue(double ratio)
    {
        switch (FuncType)
        {
            case "LinearFunc" :
                var linearFunc = SkillManager.Instance.GetLinearFunc(FuncId);
                return linearFunc?.GetValue(ratio) ?? 0;
            case "ManualFunc" :
                // Not yet implemented
                return 0;
        }
        return 0;
    }
}
