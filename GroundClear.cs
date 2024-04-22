using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;

namespace GroundClear
{
    public class WorkGiver_GroundClear : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            return pawn.Map.areaManager.SnowClear.ActiveCells;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn.Map.areaManager.SnowClear.TrueCount == 0;
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            return GetPlantOnCell(pawn, c, forced) != null;
        }

        private static Plant GetPlantOnCell(Pawn pawn, IntVec3 c, bool forced)
        {
            Plant plant = c.GetPlant(pawn.Map);
            if (plant == null)
            {
                return null;
            }
            if (c.IsForbidden(pawn) || plant.IsForbidden(pawn))
            {
                return null;
            }
            ThingDef wantedPlantDef = WorkGiver_Grower.CalculateWantedPlantDef(c, pawn.Map);
            if (plant.def == wantedPlantDef)
            {
                return null;
            }
            if (plant.def.plant.IsTree && !plant.def.plant.isStump && AreTreesDesired(pawn))
            {
                return null;
            }
            LocalTargetInfo target = plant;
            bool ignoreOtherReservations = forced;
            if (!pawn.CanReserve(target, 1, -1, null, ignoreOtherReservations))
            {
                return null;
            }
            return plant;
        }

        private static bool AreTreesDesired(Pawn pawn)
        {
            return pawn.Ideo.HasMeme(MemeDefOf.TreeConnection) || pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamed("Trees_Desired"));
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            Plant plant = GetPlantOnCell(pawn, c, forced);
            if (plant == null)
            {
                return null;
            }
            return new Job(JobDefOf.CutPlant, plant);
        }
    }

    [HarmonyPatch(typeof(Pawn), "Tick")]
    public class GroundClearTick
    {
        static Dictionary<Pawn, IntVec3> position = new Dictionary<Pawn, IntVec3>();

        static void Postfix(Pawn __instance)
        {
            Pawn pawn = __instance;
            try
            {
                if (pawn?.Position == null)
                {
                    return;
                }
                if (pawn?.Map == null)
                {
                    return;
                }
                if (!pawn.pather.Moving)
                {
                    return;
                }
                bool pawnHasNotMovedSignificantly = position.TryGetValue(pawn, out IntVec3 oldPos) && oldPos == pawn.Position;
                if (pawnHasNotMovedSignificantly)
                {
                    return;
                }
                position[pawn] = pawn.Position;
                Plant plant = GridsUtility.GetPlant(pawn.Position, pawn.Map);
                if (plant == null)
                {
                    return;
                }
                float damage = CalculatePlantDamage(pawn, plant);
                if (damage <= 0.0f)
                {
                    return;
                }
                plant.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, damage, 0f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
                // Log.Message($"{pawn.def.defName} ({pawn.Name}) caused {damage} damage to {plant.def.defName} - growth={plant.Growth}");
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce($"could not clear path: {e}", 510515686);
                return;
            }
        }

        private static float CalculatePlantDamage(Pawn pawn, Plant plant)
        {
            if (pawn == null || plant == null)
            {
                return 0;
            }
            if (plant.Growth <= 0.0f)
            {
                return 0;
            }
            if (plant.IsCrop && !pawn.HostileTo(Faction.OfPlayer))
            {
                // non-hostile pawns should not damage crops
                return 0;
            }
            if (pawn.Ideo != null)
            {
                if (pawn.Ideo.HasMeme(MemeDefOf.TreeConnection) || pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamed("Trees_Desired")))
                {
                    // pawns with tree connection or trees desired should not damage trees
                    return 0;
                }
            }
            if (plant.def.plant.IsTree && plant.HarvestableNow && pawn.BodySize < 2.0f)
            {
                // harvestable trees are considered too big to damage (unless
                // body size is 2.0 or larger)
                return 0;
            }
            float baseDamage = pawn.BodySize * pawn.GetStatValue(StatDefOf.MeleeDPS) / plant.Growth;
            if (pawn.AnimalOrWildMan() && !pawn.InAggroMentalState)
            {
                // calm animals damage plants much less
                baseDamage *= 0.1f;
            }
            if (pawn.RaceProps.FleshType == FleshTypeDefOf.Mechanoid)
            {
                // mechanoids damages plants more
                baseDamage *= 1.5f;
            }
            return baseDamage;
        }
    }

    // Gizmo patch
    [HarmonyPatch(typeof(Command))]
    [HarmonyPatch("GizmoOnGUI")]
    class PatchSnowClearDesignator
    {
        static void Postfix(Command __instance)
        {
            Designator_AreaSnowClearExpand expand = __instance as Designator_AreaSnowClearExpand;
            Designator_AreaSnowClearClear clear = __instance as Designator_AreaSnowClearClear;
            if (expand != null)
            {
                __instance.defaultLabel = "DesignatorAreaGroundClearExpand".Translate();
                __instance.defaultDesc = "DesignatorAreaGroundClearExpandDesc".Translate();
            }
            if (clear != null)
            {
                __instance.defaultLabel = "DesignatorAreaGroundClearClear".Translate();
                __instance.defaultDesc = "DesignatorAreaGroundClearClearDesc".Translate();
            }
        }
    }

    [StaticConstructorOnStartup]
    static class ClearPathPatch
    {
        static ClearPathPatch()
        {
            var harmony = new Harmony("rimworld.freemapa.clearpath");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
