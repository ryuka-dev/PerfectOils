using BepInEx.Configuration;

namespace PerfectOils
{
    internal sealed class TraitConfiguration
    {
        internal ConfigEntry<bool> RemoveDisableAiming { get; private set; }
        internal ConfigEntry<bool> RemoveMoreBulletDrop { get; private set; }
        internal ConfigEntry<bool> RemoveMoreDrag { get; private set; }
        internal ConfigEntry<bool> RemoveExtraAmmoConsumeChance { get; private set; }
        internal ConfigEntry<bool> RemoveDecreaseAccuracyWhenMoving { get; private set; }
        internal ConfigEntry<bool> RemoveDecreaseMoveSpeed { get; private set; }
        internal ConfigEntry<bool> RemoveDecreaseJumpPower { get; private set; }
        internal ConfigEntry<bool> RemoveDecreaseLootChanceMultiplier { get; private set; }
        internal ConfigEntry<bool> RemoveDisableMoneyDrops { get; private set; }
        internal ConfigEntry<bool> RemoveDisableOrganDrops { get; private set; }
        internal ConfigEntry<bool> RemoveNegativeBulletSpeed { get; private set; }
        internal ConfigEntry<bool> RemoveNegativeDamage { get; private set; }
        internal ConfigEntry<bool> RemoveNegativeBulletSize { get; private set; }
        internal ConfigEntry<bool> RemoveNegativeRpm { get; private set; }
        internal ConfigEntry<bool> RemoveNegativeMaxDurability { get; private set; }
        internal ConfigEntry<bool> RemoveMoreRecoil { get; private set; }
        internal ConfigEntry<bool> RemoveMoreSpread { get; private set; }
        internal ConfigEntry<bool> RemoveNegativeReloadSpeed { get; private set; }
        internal ConfigEntry<bool> RemoveExtraDurabilityCost { get; private set; }

        internal TraitConfiguration(ConfigFile config)
        {
            RemoveDisableAiming = config.Bind(
                "Traits",
                "RemoveDisableAiming",
                true,
                "Remove oil modifiers that disable aiming down sights.");

            RemoveMoreBulletDrop = config.Bind(
                "Traits",
                "RemoveMoreBulletDrop",
                false,
                "Remove increased projectile gravity and its same-oil speed/mass companion penalties.");

            RemoveMoreDrag = config.Bind(
                "Traits",
                "RemoveMoreDrag",
                false,
                "Remove oil modifiers that increase projectile drag.");

            RemoveExtraAmmoConsumeChance = config.Bind(
                "Traits",
                "RemoveExtraAmmoConsumeChance",
                true,
                "Remove oil modifiers that add a chance to consume extra ammunition.");

            RemoveDecreaseAccuracyWhenMoving = config.Bind(
                "Traits",
                "RemoveDecreaseAccuracyWhenMoving",
                true,
                "Remove oil modifiers that reduce accuracy while moving.");

            RemoveDecreaseMoveSpeed = config.Bind(
                "Traits",
                "RemoveDecreaseMoveSpeed",
                true,
                "Remove oil modifiers that reduce movement speed while holding the weapon.");

            RemoveDecreaseJumpPower = config.Bind(
                "Traits",
                "RemoveDecreaseJumpPower",
                true,
                "Remove oil modifiers that reduce jump power while holding the weapon.");

            RemoveDecreaseLootChanceMultiplier = config.Bind(
                "Traits",
                "RemoveDecreaseLootChanceMultiplier",
                true,
                "Remove oil modifiers that reduce the loot chance multiplier.");

            RemoveDisableMoneyDrops = config.Bind(
                "Traits",
                "RemoveDisableMoneyDrops",
                true,
                "Remove oil modifiers that disable Sulf/money drops.");

            RemoveDisableOrganDrops = config.Bind(
                "Traits",
                "RemoveDisableOrganDrops",
                true,
                "Remove oil modifiers that disable organ drops.");

            RemoveNegativeBulletSpeed = config.Bind(
                "Traits",
                "RemoveNegativeBulletSpeed",
                false,
                "Remove ProjectileTimeScale modifiers only when they reduce bullet speed. Positive bullet-speed modifiers remain active.");

            RemoveNegativeDamage = config.Bind(
                "Traits",
                "RemoveNegativeDamage",
                false,
                "Remove flat or percentage damage modifiers only when their signed value is negative. Positive damage modifiers remain active.");

            RemoveNegativeBulletSize = config.Bind(
                "Traits",
                "RemoveNegativeBulletSize",
                false,
                "Remove ProjectileScale modifiers only when they reduce bullet size. Positive bullet-size modifiers remain active.");

            RemoveNegativeRpm = config.Bind(
                "Traits",
                "RemoveNegativeRpm",
                false,
                "Remove RPM modifiers only when their signed value is negative. Positive fire-rate modifiers remain active.");

            RemoveNegativeMaxDurability = config.Bind(
                "Traits",
                "RemoveNegativeMaxDurability",
                false,
                "Remove MaxDurability modifiers only when they lower the weapon's maximum durability cap. Positive max-durability bonuses remain active.");

            RemoveMoreRecoil = config.Bind(
                "Traits",
                "RemoveMoreRecoil",
                false,
                "Remove KickMultiplier modifiers only when they increase recoil. Recoil-reducing modifiers remain active.");

            RemoveMoreSpread = config.Bind(
                "Traits",
                "RemoveMoreSpread",
                false,
                "Remove Spread modifiers only when they widen the spread cone. Accuracy-improving modifiers remain active.");

            RemoveNegativeReloadSpeed = config.Bind(
                "Traits",
                "RemoveNegativeReloadSpeed",
                false,
                "Remove ReloadSpeed modifiers only when they slow reloading. Faster-reload modifiers remain active.");

            // Keep the old section/key for backwards compatibility with v1.1.x configs.
            RemoveExtraDurabilityCost = config.Bind(
                "General",
                "RemoveExtraDurabilityCost",
                false,
                "Remove oil-specific extra durability consumption. This setting is independent from the other undesirable traits.");
        }

        internal ConfigEntry<bool>[] AllSettings()
        {
            return new[]
            {
                RemoveDisableAiming,
                RemoveMoreBulletDrop,
                RemoveMoreDrag,
                RemoveExtraAmmoConsumeChance,
                RemoveDecreaseAccuracyWhenMoving,
                RemoveDecreaseMoveSpeed,
                RemoveDecreaseJumpPower,
                RemoveDecreaseLootChanceMultiplier,
                RemoveDisableMoneyDrops,
                RemoveDisableOrganDrops,
                RemoveNegativeBulletSpeed,
                RemoveNegativeDamage,
                RemoveNegativeBulletSize,
                RemoveNegativeRpm,
                RemoveNegativeMaxDurability,
                RemoveMoreRecoil,
                RemoveMoreSpread,
                RemoveNegativeReloadSpeed,
                RemoveExtraDurabilityCost
            };
        }

        internal bool ShouldRemove(NegativeOilTrait traits)
        {
            return IsEnabled(traits, NegativeOilTrait.DisableAiming, RemoveDisableAiming) ||
                   IsEnabled(traits, NegativeOilTrait.MoreBulletDrop, RemoveMoreBulletDrop) ||
                   IsEnabled(traits, NegativeOilTrait.MoreDrag, RemoveMoreDrag) ||
                   IsEnabled(traits, NegativeOilTrait.ExtraAmmoConsumeChance, RemoveExtraAmmoConsumeChance) ||
                   IsEnabled(traits, NegativeOilTrait.DecreaseAccuracyWhenMoving, RemoveDecreaseAccuracyWhenMoving) ||
                   IsEnabled(traits, NegativeOilTrait.DecreaseMoveSpeed, RemoveDecreaseMoveSpeed) ||
                   IsEnabled(traits, NegativeOilTrait.DecreaseJumpPower, RemoveDecreaseJumpPower) ||
                   IsEnabled(traits, NegativeOilTrait.DecreaseLootChanceMultiplier, RemoveDecreaseLootChanceMultiplier) ||
                   IsEnabled(traits, NegativeOilTrait.DisableMoneyDrops, RemoveDisableMoneyDrops) ||
                   IsEnabled(traits, NegativeOilTrait.DisableOrganDrops, RemoveDisableOrganDrops) ||
                   IsEnabled(traits, NegativeOilTrait.NegativeBulletSpeed, RemoveNegativeBulletSpeed) ||
                   IsEnabled(traits, NegativeOilTrait.NegativeDamage, RemoveNegativeDamage) ||
                   IsEnabled(traits, NegativeOilTrait.NegativeBulletSize, RemoveNegativeBulletSize) ||
                   IsEnabled(traits, NegativeOilTrait.NegativeRpm, RemoveNegativeRpm) ||
                   IsEnabled(traits, NegativeOilTrait.NegativeMaxDurability, RemoveNegativeMaxDurability) ||
                   IsEnabled(traits, NegativeOilTrait.MoreRecoil, RemoveMoreRecoil) ||
                   IsEnabled(traits, NegativeOilTrait.MoreSpread, RemoveMoreSpread) ||
                   IsEnabled(traits, NegativeOilTrait.NegativeReloadSpeed, RemoveNegativeReloadSpeed) ||
                   IsEnabled(traits, NegativeOilTrait.ExtraDurabilityCost, RemoveExtraDurabilityCost);
        }

        private static bool IsEnabled(
            NegativeOilTrait traits,
            NegativeOilTrait flag,
            ConfigEntry<bool> setting)
        {
            return (traits & flag) != 0 && setting.Value;
        }
    }
}
