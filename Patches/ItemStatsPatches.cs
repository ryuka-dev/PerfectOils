using System.Reflection;
using HarmonyLib;
using PerfectRandom.Sulfur.Core.CharacterStats;
using PerfectRandom.Sulfur.Core.Stats;

namespace PerfectOils.Patches
{
    [HarmonyPatch]
    internal static class ItemStatsPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ItemStats),
                "AddModifier",
                new[] { typeof(ItemAttributes), typeof(StatModifier) });
        }

        [HarmonyPrefix]
        private static bool BeforeAddModifier(ItemAttributes __0, StatModifier __1)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null ||
                !plugin.Enabled.Value ||
                plugin.OilTraits == null)
            {
                return true;
            }

            return !plugin.OilTraits.ShouldSuppress(__0, __1);
        }
    }
}
