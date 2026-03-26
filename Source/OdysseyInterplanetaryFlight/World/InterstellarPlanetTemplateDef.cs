using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Дата-ориентированный шаблон генерации планеты.
    /// По смыслу это аналог PlanetDef/PlanetLayerGroupDef из LAO:
    /// планета описывается в Def/XML, а не жёстко в коде.
    /// </summary>
    public class InterstellarPlanetTemplateDef : Def
    {
        public float overallRainfall = 1f;
        public float overallTemperature = 1f;
        public float overallPopulation = 1f;
        public float pollution = 0f;
        public float coverage = 0.3f;
        public bool useVanillaDefaults = false;

        /// <summary>Вес шаблона при случайном выборе.</summary>
        public float selectionWeight = 1f;

        /// <summary>
        /// Для стартовой планеты можно оставить ванильные параметры.
        /// </summary>
        public bool allowedAsStartPlanet = true;

        /// <summary>
        /// Опциональный префикс для названия планеты.
        /// </summary>
        [NoTranslate]
        public string namePrefix;
    }
}
