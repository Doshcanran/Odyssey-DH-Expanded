using RimWorld;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// GameCondition для карты вакуума.
    /// Снижает температуру до -75°C и блокирует погоду.
    /// </summary>
    public class GameCondition_VoidSpace : GameCondition
    {
        public override int TransitionTicks => int.MaxValue;

        public override float TemperatureOffset()
        {
            // Базовая температура биома ~15°C + (-90) = -75°C
            return -90f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}
