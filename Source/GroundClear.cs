using System.Collections.Generic;
using System.Reflection;

using Harmony;
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
            Plant plant = c.GetPlant(pawn.Map);
            ThingDef wantedPlantDef = WorkGiver_Grower.CalculateWantedPlantDef(c, pawn.Map);
            if (plant == null || plant.def == wantedPlantDef)
            {
                return false;
            }
            if (c.IsForbidden(pawn))
            {
                return false;
            }
            LocalTargetInfo target = plant;
            bool ignoreOtherReservations = forced;
            if (!pawn.CanReserve(target, 1, -1, null, ignoreOtherReservations))
            {
                return false;
            }
            return true;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            Plant plant = c.GetPlant(pawn.Map);
            Map map = pawn.Map;
            ThingDef wantedPlantDef = WorkGiver_Grower.CalculateWantedPlantDef(c, pawn.Map);
            if (plant == null || plant.def == wantedPlantDef)
            {
                return null;
            }
            LocalTargetInfo target = plant;
    		bool ignoreOtherReservations = forced;
            if (!pawn.CanReserve(target, 1, -1, null, ignoreOtherReservations) || plant.IsForbidden(pawn))
            {
                return null;
            }
            return new Job(JobDefOf.CutPlant, plant);
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
            HarmonyInstance harmony = HarmonyInstance.Create("rimworld.freemapa.clearpath");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
