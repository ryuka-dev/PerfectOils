using System.Reflection;
using HarmonyLib;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI.ItemDescription;

namespace PerfectOils.Patches
{
    [HarmonyPatch]
    internal static class ItemDescriptionPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ItemDescription),
                "Setup",
                new[] { typeof(InventoryItem) });
        }

        [HarmonyPostfix]
        private static void AfterSetup(ItemDescription __instance, InventoryItem __0)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null ||
                !plugin.Enabled.Value ||
                !plugin.ShowRemovedTraitsWithStrikethrough.Value ||
                plugin.TooltipRenderer == null)
            {
                return;
            }

            plugin.TooltipRenderer.Apply(
                __instance,
                __0,
                plugin.DetailedLogging.Value);
        }
    }
}
