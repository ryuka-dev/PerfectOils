using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;
using PerfectRandom.Sulfur.Core.UI.ItemDescription;
using TMPro;

namespace PerfectOils
{
    internal sealed class TooltipStrikeRenderer
    {
        private enum LineKind
        {
            Attribute,
            Description
        }

        private sealed class LineSpec
        {
            internal readonly LineKind Kind;
            internal readonly string Key;
            internal readonly string Value;
            internal readonly bool IsRemoved;
            internal readonly NegativeOilTrait Traits;

            internal LineSpec(
                LineKind kind,
                string key,
                string value,
                bool isRemoved,
                NegativeOilTrait traits)
            {
                Kind = kind;
                Key = key ?? string.Empty;
                Value = value ?? string.Empty;
                IsRemoved = isRemoved;
                Traits = traits;
            }
        }

        private sealed class RenderedLine
        {
            internal readonly LineKind Kind;
            internal readonly int SiblingIndex;
            internal readonly string Key;
            internal readonly string Value;
            internal readonly TMP_Text[] TextComponents;

            internal RenderedLine(
                LineKind kind,
                int siblingIndex,
                string key,
                string value,
                TMP_Text[] textComponents)
            {
                Kind = kind;
                SiblingIndex = siblingIndex;
                Key = key ?? string.Empty;
                Value = value ?? string.Empty;
                TextComponents = textComponents;
            }
        }

        private static readonly FieldInfo AttributesInUseField =
            AccessTools.Field(typeof(ItemDescription), "attributesInUse");
        private static readonly FieldInfo DescriptionTextInUseField =
            AccessTools.Field(typeof(ItemDescription), "descriptionTextInUse");

        private static readonly FieldInfo KeyTextField =
            AccessTools.Field(typeof(ItemDescriptionAttribute), "keyText");
        private static readonly FieldInfo ValueTextField =
            AccessTools.Field(typeof(ItemDescriptionAttribute), "valueText");
        private static readonly FieldInfo ModifierTextField =
            AccessTools.Field(typeof(ItemDescriptionAttribute), "modifierText");
        private static readonly MethodInfo FormatModifierValueMethod =
            AccessTools.Method(
                typeof(ItemDescription),
                "GetValueAsStringDependingOnModType",
                new[] { typeof(ItemModifierContainer), typeof(ItemAttribute) });

        private readonly OilTraitService _oilTraits;
        private readonly TraitConfiguration _settings;
        private readonly BepInEx.Logging.ManualLogSource _log;
        private bool _loggedFirstStrikethrough;
        private bool _loggedFirstBulletDropStrikethrough;

        internal TooltipStrikeRenderer(
            OilTraitService oilTraits,
            TraitConfiguration settings,
            BepInEx.Logging.ManualLogSource log)
        {
            _oilTraits = oilTraits;
            _settings = settings;
            _log = log;
        }

        internal void Apply(
            ItemDescription description,
            InventoryItem item,
            bool detailedLogging)
        {
            if (description == null || item == null)
            {
                return;
            }

            OilTraitService.OilDefinitionInfo info;
            if (!_oilTraits.TryGetOilInfo(item, out info) ||
                info == null ||
                info.Definition == null ||
                info.Definition.modifiersApplied == null)
            {
                return;
            }

            List<LineSpec> specs = BuildLineSpecs(
                description,
                info,
                detailedLogging);
            if (specs.Count == 0)
            {
                return;
            }

            List<RenderedLine> renderedLines = CollectRenderedLines(description);
            if (renderedLines.Count == 0)
            {
                return;
            }

            bool hasRemovedBulletDropLine = false;
            for (int specIndex = 0; specIndex < specs.Count; specIndex++)
            {
                if (specs[specIndex].IsRemoved &&
                    (specs[specIndex].Traits & NegativeOilTrait.MoreBulletDrop) != 0)
                {
                    hasRemovedBulletDropLine = true;
                    break;
                }
            }

            List<int> match = FindBestOrderedMatch(renderedLines, specs);
            if (match == null)
            {
                match = FindRemovedLinesFallback(renderedLines, specs);
                if (detailedLogging)
                {
                    _log.LogWarning(
                        "[PerfectOils] Could not match the complete oil modifier block for '" +
                        SafeItemName(item) + "'; used exact removed-line fallback matching.");
                }
            }

            if (match == null)
            {
                if (hasRemovedBulletDropLine)
                {
                    _log.LogWarning(
                        "[PerfectOils] Tooltip contained a removable factor line, " +
                        "but neither ordered nor fallback matching found the rendered TMP row.");

                    for (int lineIndex = 0; lineIndex < renderedLines.Count; lineIndex++)
                    {
                        RenderedLine line = renderedLines[lineIndex];
                        _log.LogInfo(
                            "[PerfectOils] Rendered line " + lineIndex +
                            " [kind=" + line.Kind +
                            ", key='" + line.Key +
                            "', value='" + line.Value + "'].");
                    }
                }

                return;
            }

            int struck = 0;
            bool struckBulletDrop = false;
            for (int specIndex = 0; specIndex < specs.Count && specIndex < match.Count; specIndex++)
            {
                if (!specs[specIndex].IsRemoved || match[specIndex] < 0)
                {
                    continue;
                }

                ApplyStrikethrough(renderedLines[match[specIndex]]);
                struck++;

                if ((specs[specIndex].Traits & NegativeOilTrait.MoreBulletDrop) != 0)
                {
                    struckBulletDrop = true;
                }
            }

            if (struck > 0 && !_loggedFirstStrikethrough)
            {
                _loggedFirstStrikethrough = true;
                _log.LogInfo(
                    "[PerfectOils] Tooltip integration is active; removed oil traits are being shown with strikethrough.");
            }

            if (struckBulletDrop && !_loggedFirstBulletDropStrikethrough)
            {
                _loggedFirstBulletDropStrikethrough = true;
                _log.LogInfo(
                    "[PerfectOils] More Bullet Drop tooltip integration is active; " +
                    "the original Bullet Drop line is being shown with strikethrough.");
            }

            if (detailedLogging && struck > 0)
            {
                _log.LogInfo(
                    "[PerfectOils] Marked " + struck +
                    " removed trait line(s) in the tooltip for '" +
                    SafeItemName(item) + "'.");
            }
        }

        private List<LineSpec> BuildLineSpecs(
            ItemDescription description,
            OilTraitService.OilDefinitionInfo info,
            bool detailedLogging)
        {
            var specs = new List<LineSpec>();
            List<ItemModifierContainer> modifiers = info.Definition.modifiersApplied;

            for (int i = 0; i < modifiers.Count; i++)
            {
                ItemModifierContainer modifier = modifiers[i];
                if (modifier == null || modifier.attribute == ItemAttributes.None)
                {
                    continue;
                }

                ItemAttribute attribute;
                try
                {
                    attribute = AssetAccess.GetAsset(modifier.attribute);
                }
                catch
                {
                    continue;
                }

                if (attribute == null || !attribute.showInItemDescription)
                {
                    continue;
                }

                NegativeOilTrait traits = info.GetTraits(modifier);
                bool isRemoved = _settings.ShouldRemove(traits);

                if (detailedLogging &&
                    (traits & NegativeOilTrait.MoreBulletDrop) != 0)
                {
                    _log.LogInfo(
                        "[PerfectOils] More Bullet Drop tooltip modifier " +
                        "[attribute=" + modifier.attribute +
                        "(" + (int)modifier.attribute + ")" +
                        ", type=" + modifier.modType +
                        ", value=" + modifier.value +
                        ", label='" + attribute.itemDescriptionName +
                        "', isRemoved=" + isRemoved + "].");
                }

                if (attribute.simplifiedModAmount)
                {
                    bool increase = modifier.value > 0f;
                    string term = string.Format(
                        increase
                            ? "ItemAttributes/{0}_simplifiedIncrease"
                            : "ItemAttributes/{0}_simplifiedDecrease",
                        modifier.attribute);
                    string fallback = increase
                        ? attribute.simplifiedIncreaseString
                        : attribute.simplifiedDecreaseString;
                    string text = LocalizationBridge.GetTranslationOrFallback(term, fallback);
                    if (!string.IsNullOrEmpty(text))
                    {
                        specs.Add(new LineSpec(LineKind.Description, text, string.Empty, isRemoved, traits));
                    }

                    continue;
                }

                string descriptionTerm = string.Format(
                    "ItemAttributes/{0}_itemDescription",
                    modifier.attribute);
                string label = LocalizationBridge.GetTranslationOrFallback(
                    descriptionTerm,
                    attribute.itemDescriptionName);

                if (attribute.isBooleanAttribute)
                {
                    if (!string.IsNullOrEmpty(label))
                    {
                        specs.Add(new LineSpec(LineKind.Description, label, string.Empty, isRemoved, traits));
                    }

                    continue;
                }

                string value = FormatModifierValue(description, modifier, attribute);
                string displayedKey = label + (string.IsNullOrEmpty(value) ? string.Empty : ":");

                if (detailedLogging &&
                    (traits & NegativeOilTrait.MoreBulletDrop) != 0)
                {
                    _log.LogInfo(
                        "[PerfectOils] Expected More Bullet Drop tooltip line " +
                        "[key='" + displayedKey +
                        "', value='" + value +
                        "', isRemoved=" + isRemoved + "].");
                }

                specs.Add(new LineSpec(LineKind.Attribute, displayedKey, value, isRemoved, traits));
            }

            return specs;
        }

        private static string FormatModifierValue(
            ItemDescription description,
            ItemModifierContainer modifier,
            ItemAttribute attribute)
        {
            if (FormatModifierValueMethod == null)
            {
                return modifier.GetValueString();
            }

            try
            {
                return (string)FormatModifierValueMethod.Invoke(
                           description,
                           new object[] { modifier, attribute }) ??
                       string.Empty;
            }
            catch
            {
                return modifier.GetValueString() ?? string.Empty;
            }
        }

        private static List<RenderedLine> CollectRenderedLines(ItemDescription description)
        {
            var result = new List<RenderedLine>();

            var attributes = AttributesInUseField == null
                ? null
                : AttributesInUseField.GetValue(description) as List<ItemDescriptionAttribute>;
            if (attributes != null)
            {
                for (int i = 0; i < attributes.Count; i++)
                {
                    ItemDescriptionAttribute attribute = attributes[i];
                    if (attribute == null)
                    {
                        continue;
                    }

                    TMP_Text key = GetTextField(KeyTextField, attribute);
                    TMP_Text value = GetTextField(ValueTextField, attribute);
                    TMP_Text modifier = GetTextField(ModifierTextField, attribute);

                    result.Add(new RenderedLine(
                        LineKind.Attribute,
                        attribute.transform.GetSiblingIndex(),
                        GetComparableText(key),
                        GetComparableText(value),
                        new[] { key, value, modifier }));
                }
            }

            var descriptions = DescriptionTextInUseField == null
                ? null
                : DescriptionTextInUseField.GetValue(description) as List<ItemDescriptionText>;
            if (descriptions != null)
            {
                for (int i = 0; i < descriptions.Count; i++)
                {
                    ItemDescriptionText text = descriptions[i];
                    if (text == null || text.textComp == null)
                    {
                        continue;
                    }

                    result.Add(new RenderedLine(
                        LineKind.Description,
                        text.transform.GetSiblingIndex(),
                        GetComparableText(text.textComp),
                        string.Empty,
                        new[] { text.textComp }));
                }
            }

            result.Sort((left, right) => left.SiblingIndex.CompareTo(right.SiblingIndex));
            return result;
        }

        private static TMP_Text GetTextField(FieldInfo field, object instance)
        {
            return field == null ? null : field.GetValue(instance) as TMP_Text;
        }

        private static string GetComparableText(TMP_Text text)
        {
            return text == null ? string.Empty : RemoveOwnStrikeTags(text.text ?? string.Empty);
        }

        private static List<int> FindBestOrderedMatch(
            List<RenderedLine> lines,
            List<LineSpec> specs)
        {
            List<int> best = null;
            int bestSpan = int.MaxValue;

            for (int start = 0; start < lines.Count; start++)
            {
                if (!Matches(lines[start], specs[0]))
                {
                    continue;
                }

                var matched = new List<int>(specs.Count) { start };
                int cursor = start + 1;
                bool success = true;

                for (int specIndex = 1; specIndex < specs.Count; specIndex++)
                {
                    int found = -1;
                    for (int lineIndex = cursor; lineIndex < lines.Count; lineIndex++)
                    {
                        if (Matches(lines[lineIndex], specs[specIndex]))
                        {
                            found = lineIndex;
                            break;
                        }
                    }

                    if (found < 0)
                    {
                        success = false;
                        break;
                    }

                    matched.Add(found);
                    cursor = found + 1;
                }

                if (!success)
                {
                    continue;
                }

                int span = matched[matched.Count - 1] - matched[0];
                if (span < bestSpan)
                {
                    best = matched;
                    bestSpan = span;
                }
            }

            return best;
        }

        private static List<int> FindRemovedLinesFallback(
            List<RenderedLine> lines,
            List<LineSpec> specs)
        {
            var matched = new List<int>(specs.Count);
            var used = new HashSet<int>();
            bool foundAny = false;

            for (int specIndex = 0; specIndex < specs.Count; specIndex++)
            {
                if (!specs[specIndex].IsRemoved)
                {
                    matched.Add(-1);
                    continue;
                }

                int found = -1;
                for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    if (!used.Contains(lineIndex) && Matches(lines[lineIndex], specs[specIndex]))
                    {
                        found = lineIndex;
                        used.Add(lineIndex);
                        foundAny = true;
                        break;
                    }
                }

                matched.Add(found);
            }

            return foundAny ? matched : null;
        }

        private static bool Matches(RenderedLine line, LineSpec spec)
        {
            return line.Kind == spec.Kind &&
                   string.Equals(line.Key, spec.Key, StringComparison.Ordinal) &&
                   string.Equals(line.Value, spec.Value, StringComparison.Ordinal);
        }

        private static void ApplyStrikethrough(RenderedLine line)
        {
            for (int i = 0; i < line.TextComponents.Length; i++)
            {
                TMP_Text text = line.TextComponents[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                string plain = RemoveOwnStrikeTags(text.text);
                text.text = "<s>" + plain + "</s>";
            }
        }

        private static string RemoveOwnStrikeTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            const string open = "<s>";
            const string close = "</s>";
            if (text.StartsWith(open, StringComparison.Ordinal) &&
                text.EndsWith(close, StringComparison.Ordinal))
            {
                return text.Substring(open.Length, text.Length - open.Length - close.Length);
            }

            return text;
        }

        private static string SafeItemName(InventoryItem item)
        {
            if (item == null || item.itemDefinition == null)
            {
                return "<unknown oil>";
            }

            return string.IsNullOrEmpty(item.itemDefinition.displayName)
                ? item.itemDefinition.ToString()
                : item.itemDefinition.displayName;
        }

        private static class LocalizationBridge
        {
            private static readonly MethodInfo TryGetTranslationMethod = FindTryGetTranslation();

            internal static string GetTranslationOrFallback(string term, string fallback)
            {
                if (TryGetTranslationMethod != null)
                {
                    try
                    {
                        object[] arguments =
                        {
                            term,
                            null,
                            true,
                            0,
                            true,
                            false,
                            null,
                            null,
                            true
                        };

                        bool success = (bool)TryGetTranslationMethod.Invoke(null, arguments);
                        string translated = arguments[1] as string;
                        if (success && !string.IsNullOrEmpty(translated))
                        {
                            return translated;
                        }
                    }
                    catch
                    {
                        // Fall through to the serialized English/default text.
                    }
                }

                return fallback ?? string.Empty;
            }

            private static MethodInfo FindTryGetTranslation()
            {
                Type localizationManager = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                if (localizationManager == null)
                {
                    return null;
                }

                MethodInfo[] methods = localizationManager.GetMethods(
                    BindingFlags.Public | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != "TryGetTranslation")
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 9 &&
                        parameters[0].ParameterType == typeof(string) &&
                        parameters[1].ParameterType == typeof(string).MakeByRefType())
                    {
                        return method;
                    }
                }

                return null;
            }
        }
    }
}
