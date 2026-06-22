using System;
using System.Collections.Generic;
using System.Text;
using PerfectRandom.Sulfur.Core.CharacterStats;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;

namespace PerfectOils
{
    [Flags]
    internal enum NegativeOilTrait
    {
        None = 0,
        DisableAiming = 1 << 0,
        MoreBulletDrop = 1 << 1,
        MoreDrag = 1 << 2,
        ExtraAmmoConsumeChance = 1 << 3,
        DecreaseAccuracyWhenMoving = 1 << 4,
        DecreaseMoveSpeed = 1 << 5,
        DecreaseJumpPower = 1 << 6,
        DecreaseLootChanceMultiplier = 1 << 7,
        DisableMoneyDrops = 1 << 8,
        DisableOrganDrops = 1 << 9,
        NegativeBulletSpeed = 1 << 10,
        NegativeDamage = 1 << 11,
        NegativeBulletSize = 1 << 12,
        NegativeRpm = 1 << 13,
        ExtraDurabilityCost = 1 << 14
    }

    internal static class NegativeTraitPolicy
    {
        private const float Epsilon = 0.000001f;

        /// <summary>
        /// Identifies the actual modifier used by the game to render the
        /// "Bullet drop" tooltip row. The current SULFUR data does not use
        /// ProjectileGravityFactor for the visible xN Bullet Drop trait, so
        /// the semantic item-attribute metadata is the stable source of truth.
        /// </summary>
        internal static bool IsMoreBulletDropMarker(
            ItemModifierContainer modifier,
            ItemAttribute attribute)
        {
            return modifier != null &&
                   attribute != null &&
                   attribute.showInItemDescription &&
                   IncreasesAttribute(modifier) &&
                   DescribesBulletDrop(attribute);
        }

        internal static NegativeOilTrait Classify(
            ItemModifierContainer modifier,
            bool oilHasMoreBulletDrop,
            ItemAttribute attribute)
        {
            if (modifier == null)
            {
                return NegativeOilTrait.None;
            }

            // This check must happen before the enum switch because the visible
            // Bullet Drop line is backed by a different runtime attribute in
            // the current asset database.
            if (IsMoreBulletDropMarker(modifier, attribute))
            {
                return NegativeOilTrait.MoreBulletDrop;
            }

            switch (modifier.attribute)
            {
                case ItemAttributes.DisableADS:
                    return IncreasesAttribute(modifier)
                        ? NegativeOilTrait.DisableAiming
                        : NegativeOilTrait.None;

                case ItemAttributes.ProjectileDrag:
                    return IncreasesAttribute(modifier)
                        ? NegativeOilTrait.MoreDrag
                        : NegativeOilTrait.None;

                case ItemAttributes.ConsumeExtraAmmoChance:
                    return IncreasesAttribute(modifier)
                        ? NegativeOilTrait.ExtraAmmoConsumeChance
                        : NegativeOilTrait.None;

                case ItemAttributes.AimMovingBonus:
                    return DecreasesAttribute(modifier)
                        ? NegativeOilTrait.DecreaseAccuracyWhenMoving
                        : NegativeOilTrait.None;

                case ItemAttributes.ItemStat_MoveSpeed:
                    return DecreasesAttribute(modifier)
                        ? NegativeOilTrait.DecreaseMoveSpeed
                        : NegativeOilTrait.None;

                case ItemAttributes.ItemStat_JumpPower:
                    return DecreasesAttribute(modifier)
                        ? NegativeOilTrait.DecreaseJumpPower
                        : NegativeOilTrait.None;

                case ItemAttributes.ItemStat_LootChanceMultiplier:
                    return DecreasesAttribute(modifier)
                        ? NegativeOilTrait.DecreaseLootChanceMultiplier
                        : NegativeOilTrait.None;

                case ItemAttributes.DisableLootMoney:
                    return IncreasesAttribute(modifier)
                        ? NegativeOilTrait.DisableMoneyDrops
                        : NegativeOilTrait.None;

                case ItemAttributes.DisableLootOrgans:
                    return IncreasesAttribute(modifier)
                        ? NegativeOilTrait.DisableOrganDrops
                        : NegativeOilTrait.None;

                case ItemAttributes.ProjectileTimeScale:
                    if (!DecreasesAttribute(modifier))
                    {
                        return NegativeOilTrait.None;
                    }

                    // Reduced projectile speed is independently configurable.
                    // If the same oil contains the visible Bullet Drop marker,
                    // either setting may suppress this shared companion penalty.
                    return NegativeOilTrait.NegativeBulletSpeed |
                           (oilHasMoreBulletDrop
                               ? NegativeOilTrait.MoreBulletDrop
                               : NegativeOilTrait.None);

                case ItemAttributes.ProjectileMass:
                    return oilHasMoreBulletDrop && IncreasesAttribute(modifier)
                        ? NegativeOilTrait.MoreBulletDrop
                        : NegativeOilTrait.None;

                case ItemAttributes.Damage:
                case ItemAttributes.DamageModifier:
                    return DecreasesAttribute(modifier)
                        ? NegativeOilTrait.NegativeDamage
                        : NegativeOilTrait.None;

                case ItemAttributes.ProjectileScale:
                    return DecreasesAttribute(modifier)
                        ? NegativeOilTrait.NegativeBulletSize
                        : NegativeOilTrait.None;

                case ItemAttributes.RPM:
                    return DecreasesAttribute(modifier)
                        ? NegativeOilTrait.NegativeRpm
                        : NegativeOilTrait.None;

                case ItemAttributes.EnchantmentDurabilityCost:
                    return IncreasesAttribute(modifier)
                        ? NegativeOilTrait.ExtraDurabilityCost
                        : NegativeOilTrait.None;

                default:
                    return NegativeOilTrait.None;
            }
        }

        internal static string Describe(NegativeOilTrait traits)
        {
            if (traits == NegativeOilTrait.None)
            {
                return "None";
            }

            var names = new List<string>();
            AddName(names, traits, NegativeOilTrait.DisableAiming, "Disable Aiming");
            AddName(names, traits, NegativeOilTrait.MoreBulletDrop, "More Bullet Drop");
            AddName(names, traits, NegativeOilTrait.MoreDrag, "More Drag");
            AddName(names, traits, NegativeOilTrait.ExtraAmmoConsumeChance, "Extra Ammo Consume Chance");
            AddName(names, traits, NegativeOilTrait.DecreaseAccuracyWhenMoving, "Decrease Accuracy When Moving");
            AddName(names, traits, NegativeOilTrait.DecreaseMoveSpeed, "Decrease Move Speed");
            AddName(names, traits, NegativeOilTrait.DecreaseJumpPower, "Decrease Jump Power");
            AddName(names, traits, NegativeOilTrait.DecreaseLootChanceMultiplier, "Decrease Loot Chance Multiplier");
            AddName(names, traits, NegativeOilTrait.DisableMoneyDrops, "Disable Money Drops");
            AddName(names, traits, NegativeOilTrait.DisableOrganDrops, "Disable Organ Drops");
            AddName(names, traits, NegativeOilTrait.NegativeBulletSpeed, "Negative Bullet Speed");
            AddName(names, traits, NegativeOilTrait.NegativeDamage, "Negative Damage");
            AddName(names, traits, NegativeOilTrait.NegativeBulletSize, "Negative Bullet Size");
            AddName(names, traits, NegativeOilTrait.NegativeRpm, "Negative RPM");
            AddName(names, traits, NegativeOilTrait.ExtraDurabilityCost, "Extra Oil Durability Cost");
            return string.Join(" + ", names.ToArray());
        }

        private static bool DescribesBulletDrop(ItemAttribute attribute)
        {
            return ContainsBulletDropMeaning(attribute.itemDescriptionName) ||
                   ContainsBulletDropMeaning(attribute.simplifiedIncreaseString);
        }

        private static bool ContainsBulletDropMeaning(string value)
        {
            string normalized = NormalizeSemanticText(value);
            return normalized.Contains("bulletdrop") ||
                   normalized.Contains("projectiledrop") ||
                   normalized.Contains("bulletfall") ||
                   normalized.Contains("projectilefall");
        }

        private static string NormalizeSemanticText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static bool IncreasesAttribute(ItemModifierContainer modifier)
        {
            return IsSupportedModType(modifier.modType) && modifier.value > Epsilon;
        }

        private static bool DecreasesAttribute(ItemModifierContainer modifier)
        {
            return IsSupportedModType(modifier.modType) && modifier.value < -Epsilon;
        }

        private static bool IsSupportedModType(StatModType modType)
        {
            return modType == StatModType.Flat ||
                   modType == StatModType.PercentAdd ||
                   modType == StatModType.PercentMult;
        }

        private static void AddName(
            List<string> names,
            NegativeOilTrait traits,
            NegativeOilTrait flag,
            string name)
        {
            if ((traits & flag) != 0)
            {
                names.Add(name);
            }
        }
    }
}
