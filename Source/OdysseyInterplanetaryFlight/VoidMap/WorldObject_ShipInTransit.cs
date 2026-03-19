using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Мировой объект — корабль в межпланетном перелёте.
    /// Привязан к временному тайлу и содержит карту вакуума.
    /// </summary>
    public class WorldObject_ShipInTransit : MapParent
    {
        public int transitShipThingId = -1;

        public override string Label
        {
            get
            {
                WorldComponent_Interstellar data = Find.World?.GetComponent<WorldComponent_Interstellar>();
                if (data != null)
                {
                    var record = data.activeTravels?.Find(r => r?.shipThingId == transitShipThingId);
                    if (record != null)
                        return (record.shipLabel ?? "Корабль") + " [в полёте]";
                }
                return "Корабль [в полёте]";
            }
        }

        // Предотвращаем ошибку "null material" — возвращаем безопасный материал
        public override Material Material
        {
            get
            {
                // Пробуем найти иконку корабля из ванили
                Texture2D tex = ContentFinder<Texture2D>.Get("UI/Overlays/ExpandingIcons/Ship", false)
                             ?? ContentFinder<Texture2D>.Get("UI/Icons/ColonistBar/MoodNeutral", false)
                             ?? BaseContent.BadTex;

                return MaterialPool.MatFrom(new MaterialRequest(tex, ShaderDatabase.WorldOverlayTransparentLit));
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = false;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref transitShipThingId, "transitShipThingId", -1);
        }
    }
}
