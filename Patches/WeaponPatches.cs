using HarmonyLib;
using PerfectRandom.Sulfur.Core.Weapons;

namespace PerfectOils.Patches
{
    [HarmonyPatch(typeof(Weapon), "SyncEnchantments")]
    internal static class WeaponPatches
    {
        [HarmonyPrefix]
        private static void ResetCachedAimingState(ref bool ___aimingDisabled)
        {
            // The original method sets this field when DisableADS is present but does
            // not clear it when the modifier disappears. Reset first, then let the
            // original method calculate the current state normally.
            ___aimingDisabled = false;
        }
    }
}
