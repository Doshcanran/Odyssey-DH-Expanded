using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OdysseyInterplanetaryFlight.World
{
    public static class InterstellarPlanetGenerationUtility
    {
        private static readonly string[] NamePrefix =
        {
            "Kepler", "Tau", "Sigma", "Nova", "Helios",
            "Epsilon", "Vega", "Orion", "Atlas", "Hyperion"
        };

        private static readonly string[] NameSuffix =
        {
            "I", "II", "III", "IV", "V",
            "Prime", "Beta", "Gamma", "Delta", "Secundus"
        };

        /// <summary>
        /// Детерминированная генерация имени планеты (без внешних зависимостей)
        /// </summary>
        private static string GeneratePlanetNameDeterministic(int seed, int index)
        {
            unchecked
            {
                int localSeed = Gen.HashCombineInt(seed, index);

                Rand.PushState(localSeed);

                string prefix = NamePrefix[Math.Abs(localSeed) % NamePrefix.Length];
                string suffix = NameSuffix[Math.Abs(localSeed / 7) % NameSuffix.Length];
                int numeric = 100 + Math.Abs(localSeed % 900);

                string result = $"{prefix} {numeric}-{suffix}";

                Rand.PopState();
                return result;
            }
        }

        /// <summary>
        /// Пример основной генерации планеты
        /// </summary>
        public static GeneratedPlanet GeneratePlanet(int seed, int planetIndex)
        {
            var planet = new GeneratedPlanet();

            planet.seed = Gen.HashCombineInt(seed, planetIndex);
            planet.name = GeneratePlanetNameDeterministic(seed, planetIndex);

            // Примитивная процедурная генерация параметров
            Rand.PushState(planet.seed);

            planet.temperature = Rand.Range(-40f, 60f);
            planet.rainfall = Rand.Range(0f, 4000f);
            planet.toxicity = Rand.Value;
            planet.hasLife = Rand.Value > 0.3f;

            Rand.PopState();

            return planet;
        }
    }

    /// <summary>
    /// DTO под генерацию (если у тебя уже есть свой — удали этот класс)
    /// </summary>
    public class GeneratedPlanet
    {
        public int seed;
        public string name;

        public float temperature;
        public float rainfall;
        public float toxicity;
        public bool hasLife;
    }
}