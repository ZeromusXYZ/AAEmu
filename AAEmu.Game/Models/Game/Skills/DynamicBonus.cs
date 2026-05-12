using AAEmu.Game.Models.Game.Skills.Templates;

namespace AAEmu.Game.Models.Game.Skills;

public class DynamicBonus
{
    public DynamicBonusTemplate Template { get; set; }
    public Buff SourceBuff { get; set; }

    public bool Evaluate(out double value)
    {
        if (SourceBuff == null)
        {
            value = 0;
            return false;
        }

        var remainingTime = (SourceBuff.EndTime - DateTime.UtcNow).TotalMilliseconds;
        var ratio = SourceBuff.Duration > 0 ? Math.Clamp((remainingTime / SourceBuff.Duration), 0, 1) : 0;
        value = Template.GetValue(ratio);
        return true;
    }
}
