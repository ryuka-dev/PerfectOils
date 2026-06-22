using HarmonyLib;
using PerfectRandom.Sulfur.Core;

namespace PerfectOils.Patches
{
    [HarmonyPatch(typeof(AsyncAssetLoading), "set_loadingDone")]
    internal static class AssetLoadingPatches
    {
        [HarmonyPostfix]
        private static void AfterLoadingDoneSet(AsyncAssetLoading __instance, bool value)
        {
            Plugin plugin = Plugin.Instance;
            if (!value || plugin == null)
            {
                return;
            }

            plugin.NotifyAssetsReady(__instance);
        }
    }
}
