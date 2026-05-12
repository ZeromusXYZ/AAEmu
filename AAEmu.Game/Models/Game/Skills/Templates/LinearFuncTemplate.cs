using Jace.Util;

namespace AAEmu.Game.Models.Game.Skills.Templates;

public class LinearFuncTemplate
{
    public uint Id { get; set; }
    public int StartValue { get; set; }
    public int EndValue { get; set; }

    public double GetValue(double ratio)
    {
        return StartValue + ((EndValue - StartValue) * ratio);
    }
}
