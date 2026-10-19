using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using UnityEngine;

namespace SkillEffectsExtended;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(SkillsExtendedGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.musicmaniac.skilleffectsextended";
    private const string PluginName = "Skill Effects Extended";
    private const string PluginVersion = "1.0.0";
    private const string SkillsExtendedGuid = "com.cj.skillsextended";

    internal static bool SkillsExtendedAvailable { get; private set; }
    internal static ConfigEntry<float> MagazineErgonomicsPercentPerLevel = null!;
    internal static ConfigEntry<float> MagazineRecoilPercentPerLevel = null!;
    internal static ConfigEntry<float> SuppressorErgonomicsPercentPerLevel = null!;
    internal static ConfigEntry<float> SightErgonomicsPercentPerLevel = null!;
    internal static ConfigEntry<float> HeadgearErgonomicsPercentPerLevel = null!;
    internal static ConfigEntry<float> LightArmorErgonomicsPercentPerLevel = null!;
    internal static ConfigEntry<float> HeavyArmorErgonomicsPercentPerLevel = null!;
    internal static ConfigEntry<float> BackpackMovementPercentPerLevel = null!;
    internal static ConfigEntry<float> BackpackTurnPercentPerLevel = null!;
    internal static ConfigEntry<float> BackpackErgonomicsPercentPerLevel = null!;

    private void Awake()
    {
        SkillsExtendedAvailable = Chainloader.PluginInfos.ContainsKey(SkillsExtendedGuid);

        MagazineErgonomicsPercentPerLevel = BindReduction(
            "Mag Drills - Magazine Ergonomics", "ReductionPercentPerLevel", 1f,
            "Percentage of a magazine's negative ergonomics modifier removed per Mag Drills level.");
        MagazineRecoilPercentPerLevel = BindReduction(
            "Mag Drills - Magazine Recoil", "ReductionPercentPerLevel", 1f,
            "Percentage of a magazine's positive recoil penalty removed per Mag Drills level.");
        SuppressorErgonomicsPercentPerLevel = BindReduction(
            "Silent Ops - Suppressor Ergonomics", "ReductionPercentPerLevel", 1f,
            "Percentage of a suppressor's negative ergonomics modifier removed per Silent Ops level. Requires Skills Extended.");
        SightErgonomicsPercentPerLevel = BindReduction(
            "Aim Drills - Sight Ergonomics", "ReductionPercentPerLevel", 1f,
            "Percentage of a sight's negative ergonomics modifier removed per Aim Drills level.");
        HeadgearErgonomicsPercentPerLevel = BindReduction(
            "Aim Drills - Headgear Ergonomics", "ReductionPercentPerLevel", 1f,
            "Percentage of headwear, facecover, or helmet attachment ergonomics penalty removed per Aim Drills level.");
        LightArmorErgonomicsPercentPerLevel = BindReduction(
            "Light Vests - Armor Ergonomics", "ReductionPercentPerLevel", 1f,
            "Percentage of light armor's negative ergonomics penalty removed per Light Vests level.");
        HeavyArmorErgonomicsPercentPerLevel = BindReduction(
            "Heavy Vests - Armor Ergonomics", "ReductionPercentPerLevel", 1f,
            "Percentage of heavy armor's negative ergonomics penalty removed per Heavy Vests level.");
        BackpackMovementPercentPerLevel = BindReduction(
            "Endurance - Backpack Movement Speed", "ReductionPercentPerLevel", 1f,
            "Percentage of a backpack's negative movement speed penalty removed per Endurance level.");
        BackpackTurnPercentPerLevel = BindReduction(
            "Strength - Backpack Turn Speed", "ReductionPercentPerLevel", 1f,
            "Percentage of a backpack's negative turn speed penalty removed per Strength level.");
        BackpackErgonomicsPercentPerLevel = BindReduction(
            "Strength - Backpack Ergonomics", "ReductionPercentPerLevel", 1f,
            "Percentage of a backpack's negative ergonomics penalty removed per Strength level.");

        new ModErgonomicsPatch().Enable();
        new ArmorComponentErgonomicsPatch().Enable();
        new ArmorPenaltyItemDisplayPatch().Enable();
        new MagazineRecoilPatch().Enable();
        new BackpackMovementPenaltyPatch().Enable();
        new BackpackTurnPenaltyPatch().Enable();
        new BackpackErgonomicsPenaltyPatch().Enable();
        TryEnableUiPatch(new BuffDescriptionPatch());
        TryEnableUiPatch(new SkillTooltipBuffsPatch());
        TryEnableUiPatch(new SkillPanelBuffsPatch());

        Logger.LogInfo(
            $"Loaded. Skills Extended: {(SkillsExtendedAvailable ? "detected" : "not detected")}; " +
            $"Mag Drills magazine ergonomics {MagazineErgonomicsPercentPerLevel.Value:0.##}%/level; " +
            $"magazine recoil {MagazineRecoilPercentPerLevel.Value:0.##}%/level; " +
            $"Silent Ops suppressor ergonomics {SuppressorErgonomicsPercentPerLevel.Value:0.##}%/level; " +
            $"Aim Drills sight/headgear ergonomics " +
            $"{SightErgonomicsPercentPerLevel.Value:0.##}%/{HeadgearErgonomicsPercentPerLevel.Value:0.##}% per level; " +
            $"light/heavy armor ergonomics " +
            $"{LightArmorErgonomicsPercentPerLevel.Value:0.##}%/{HeavyArmorErgonomicsPercentPerLevel.Value:0.##}% per level; " +
            $"backpack movement/turn/ergonomics reductions " +
            $"{BackpackMovementPercentPerLevel.Value:0.##}%/{BackpackTurnPercentPerLevel.Value:0.##}%/" +
            $"{BackpackErgonomicsPercentPerLevel.Value:0.##}% per level.");
    }

    private ConfigEntry<float> BindReduction(string section, string key, float defaultValue, string description) =>
        Config.Bind(section, key, defaultValue,
            new ConfigDescription(description, new AcceptableValueRange<float>(0f, 100f)));

    private void TryEnableUiPatch(ModulePatch patch)
    {
        try
        {
            patch.Enable();
        }
        catch (Exception exception)
        {
            Logger.LogError($"Could not enable optional UI patch {patch.GetType().Name}: {exception}");
        }
    }

    internal static float GetMagazineErgonomicsReductionPercent(int level) =>
        GetReductionPercent(level, MagazineErgonomicsPercentPerLevel.Value);

    internal static float GetMagazineRecoilReductionPercent(int level) =>
        GetReductionPercent(level, MagazineRecoilPercentPerLevel.Value);

    internal static float GetSuppressorErgonomicsReductionPercent(int level) =>
        GetReductionPercent(level, SuppressorErgonomicsPercentPerLevel.Value);

    internal static float GetSightErgonomicsReductionPercent(int level) =>
        GetReductionPercent(level, SightErgonomicsPercentPerLevel.Value);

    internal static float GetHeadgearErgonomicsReductionPercent(int level) =>
        GetReductionPercent(level, HeadgearErgonomicsPercentPerLevel.Value);

    internal static float GetLightArmorErgonomicsReductionPercent(int level) =>
        GetReductionPercent(level, LightArmorErgonomicsPercentPerLevel.Value);

    internal static float GetHeavyArmorErgonomicsReductionPercent(int level) =>
        GetReductionPercent(level, HeavyArmorErgonomicsPercentPerLevel.Value);

    internal static float GetBackpackMovementReductionPercent(int level) =>
        GetReductionPercent(level, BackpackMovementPercentPerLevel.Value);

    internal static float GetBackpackTurnReductionPercent(int level) =>
        GetReductionPercent(level, BackpackTurnPercentPerLevel.Value);

    internal static float GetBackpackErgonomicsReductionPercent(int level) =>
        GetReductionPercent(level, BackpackErgonomicsPercentPerLevel.Value);

    internal static bool TryGetSilentOps(SkillManager skills, out Skill silentOps)
    {
        silentOps = skills.SilentOps;
        return SkillsExtendedAvailable && silentOps is not null && !silentOps.Locked;
    }

    private static float GetReductionPercent(int level, float percentPerLevel)
    {
        return Math.Max(0f, Math.Min(100f, Math.Max(0, level) * percentPerLevel));
    }
}

internal sealed class ModErgonomicsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.PropertyGetter(typeof(Mod), nameof(Mod.Ergonomics));

    [PatchPostfix]
    private static void Postfix(Mod __instance, ref float __result)
    {
        if (__result >= 0f)
        {
            return;
        }

        SkillManager? skills = ClientAppUtils.GetClientApp()?.Session?.Profile?.Skills;
        if (skills is null)
        {
            return;
        }

        float reductionPercent;
        if (__instance is Magazine)
        {
            reductionPercent = Plugin.GetMagazineErgonomicsReductionPercent(skills.MagDrills.Level);
        }
        else if (__instance is Silencer && Plugin.TryGetSilentOps(skills, out Skill silentOps))
        {
            reductionPercent = Plugin.GetSuppressorErgonomicsReductionPercent(silentOps.Level);
        }
        else if (__instance is SightMod)
        {
            reductionPercent = Plugin.GetSightErgonomicsReductionPercent(skills.AimDrills.Level);
        }
        else if (ArmorPenaltyReduction.IsHeadwearAttachment(__instance))
        {
            reductionPercent = Plugin.GetHeadgearErgonomicsReductionPercent(skills.AimDrills.Level);
        }
        else
        {
            return;
        }

        __result *= 1f - reductionPercent / 100f;
    }
}

internal static class ArmorPenaltyReduction
{
    internal static float GetErgonomicsPercent(ArmorComponent armor, SkillManager skills)
    {
        return GetErgonomicsPercent(armor.Item, armor.Template, armor.ArmorType, skills);
    }

    internal static float GetErgonomicsPercent(
        Item item,
        IEquipmentPenaltyComponentTemplate template,
        EArmorType fallbackType,
        SkillManager skills)
    {
        if (item is Headwear || template is HeadwearTemplate ||
            item is FaceCover || template is FaceCoverTemplate || IsHeadwearAttachment(item))
        {
            return Plugin.GetHeadgearErgonomicsReductionPercent(skills.AimDrills.Level);
        }

        EArmorType armorType = GetSoftArmorType(item, fallbackType, template);
        return armorType switch
        {
            EArmorType.Light => Plugin.GetLightArmorErgonomicsReductionPercent(skills.LightVests.Level),
            EArmorType.Heavy => Plugin.GetHeavyArmorErgonomicsReductionPercent(skills.HeavyVests.Level),
            _ => 0f
        };
    }

    internal static float ReduceParentErgonomicsPenalty(
        float totalPenalty,
        Item item,
        IEquipmentPenaltyComponentTemplate template)
    {
        if (template.WeaponErgonomicPenalty >= 0f)
        {
            return totalPenalty;
        }

        SkillManager? skills = ClientAppUtils.GetClientApp()?.Session?.Profile?.Skills;
        if (skills is null)
        {
            return totalPenalty;
        }

        EArmorType fallbackType = EArmorType.None;
        if (item.TryGetItemComponent<ArmorComponent>(out ArmorComponent directArmor))
        {
            fallbackType = directArmor.ArmorType;
        }

        float reductionPercent = GetErgonomicsPercent(item, template, fallbackType, skills);
        return totalPenalty - template.WeaponErgonomicPenalty * reductionPercent / 100f;
    }

    private static EArmorType GetSoftArmorType(
        Item item,
        EArmorType fallbackType,
        IEquipmentPenaltyComponentTemplate template)
    {
        if (fallbackType != EArmorType.None)
        {
            return fallbackType;
        }

        EArmorType templateType = ReadArmorType(template) ??
            ReadArmorType(AccessTools.Property(item.GetType(), "Template")?.GetValue(item)) ??
            EArmorType.None;
        if (templateType != EArmorType.None)
        {
            return templateType;
        }

        foreach (Item child in item.GetAllVisibleItems())
        {
            if (child.TryGetItemComponent<ArmorComponent>(out ArmorComponent armor) &&
                armor.ArmorType != EArmorType.None)
            {
                return armor.ArmorType;
            }
        }

        return EArmorType.None;
    }

    private static EArmorType? ReadArmorType(object? source)
    {
        if (source is null)
        {
            return null;
        }

        object? value = AccessTools.Property(source.GetType(), "ArmorType")?.GetValue(source) ??
            AccessTools.Field(source.GetType(), "ArmorType")?.GetValue(source);
        if (value is EArmorType armorType)
        {
            return armorType;
        }

        return value is not null && Enum.TryParse(value.ToString(), true, out EArmorType parsed)
            ? parsed
            : null;
    }

    internal static bool IsHeadwearAttachment(Item item)
    {
        object? current = item;
        for (int depth = 0; depth < 8 && current is not null; depth++)
        {
            object? parent;
            try
            {
                parent = AccessTools.Property(current.GetType(), "Parent")?.GetValue(current);
            }
            catch (TargetInvocationException)
            {
                return false;
            }

            if (parent is null)
            {
                return false;
            }

            Item? parentItem = parent as Item ??
                AccessTools.Property(parent.GetType(), "Item")?.GetValue(parent) as Item ??
                AccessTools.Property(parent.GetType(), "ParentItem")?.GetValue(parent) as Item ??
                AccessTools.Field(parent.GetType(), "ParentItem")?.GetValue(parent) as Item;
            if (parentItem is null)
            {
                object? slot = AccessTools.Field(parent.GetType(), "Slot")?.GetValue(parent);
                parentItem = slot is null
                    ? null
                    : AccessTools.Property(slot.GetType(), "ParentItem")?.GetValue(slot) as Item;
            }
            if (parentItem is null)
            {
                object? grid = AccessTools.Field(parent.GetType(), "Grid")?.GetValue(parent);
                parentItem = grid is null
                    ? null
                    : AccessTools.Property(grid.GetType(), "ParentItem")?.GetValue(grid) as Item;
            }
            if (parentItem is null)
            {
                object? container = AccessTools.Field(parent.GetType(), "Container")?.GetValue(parent);
                parentItem = container is null
                    ? null
                    : AccessTools.Property(container.GetType(), "Item")?.GetValue(container) as Item;
            }
            if (parentItem is null)
            {
                return false;
            }

            if (parentItem is Headwear)
            {
                return true;
            }

            current = parentItem;
        }

        return false;
    }
}

internal sealed class ArmorComponentErgonomicsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.PropertyGetter(typeof(ArmorComponent), nameof(ArmorComponent.WeaponErgonomicPenalty));

    [PatchPostfix]
    private static void Postfix(ArmorComponent __instance, ref float __result)
    {
        if (__result >= 0f)
        {
            return;
        }

        SkillManager? skills = ClientAppUtils.GetClientApp()?.Session?.Profile?.Skills;
        if (skills is null)
        {
            return;
        }

        float reductionPercent = ArmorPenaltyReduction.GetErgonomicsPercent(__instance, skills);
        __result *= 1f - reductionPercent / 100f;
    }
}

internal sealed class ArmorPenaltyItemDisplayPatch : ModulePatch
{
    private static readonly ConditionalWeakTable<ItemAttribute, object> WrappedAttributes = new();

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Constructor(typeof(EquipmentPenaltyComponent),
            new[] { typeof(Item), typeof(IEquipmentPenaltyComponentTemplate), typeof(bool) });

    [PatchPostfix]
    private static void Postfix(EquipmentPenaltyComponent __instance, object[] __args)
    {
        if (__args.Length < 2 || __args[0] is not Item item ||
            __args[1] is not IEquipmentPenaltyComponentTemplate template)
        {
            return;
        }

        foreach (ItemAttribute attribute in item.Attributes)
        {
            bool isErgonomics = attribute.Id.Equals(EItemAttributeId.Ergonomics);
            if (!isErgonomics ||
                attribute.Base is not Func<float> originalBase ||
                WrappedAttributes.TryGetValue(attribute, out _))
            {
                continue;
            }

            WrappedAttributes.Add(attribute, new object());
            attribute.Base = () => ArmorPenaltyReduction.ReduceParentErgonomicsPenalty(originalBase(), item, template);
            attribute.StringValue = () => string.Format("{0} %", attribute.Base());
        }
    }
}

internal sealed class BackpackMovementPenaltyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BackpackTemplate),
            "EFT.InventoryLogic.IEquipmentPenaltyComponentTemplate.get_SpeedPenaltyPercent");

    [PatchPostfix]
    private static void Postfix(ref float __result)
    {
        if (__result >= 0f)
        {
            return;
        }

        SkillManager? skills = ClientAppUtils.GetClientApp()?.Session?.Profile?.Skills;
        if (skills is null)
        {
            return;
        }

        float reductionPercent = Plugin.GetBackpackMovementReductionPercent(skills.Endurance.Level);
        __result *= 1f - reductionPercent / 100f;
    }
}

internal sealed class BackpackTurnPenaltyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BackpackTemplate),
            "EFT.InventoryLogic.IEquipmentPenaltyComponentTemplate.get_MousePenalty");

    [PatchPostfix]
    private static void Postfix(ref float __result)
    {
        if (__result >= 0f)
        {
            return;
        }

        SkillManager? skills = ClientAppUtils.GetClientApp()?.Session?.Profile?.Skills;
        if (skills is null)
        {
            return;
        }

        float reductionPercent = Plugin.GetBackpackTurnReductionPercent(skills.Strength.Level);
        __result *= 1f - reductionPercent / 100f;
    }
}

internal sealed class BackpackErgonomicsPenaltyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BackpackTemplate),
            "EFT.InventoryLogic.IEquipmentPenaltyComponentTemplate.get_WeaponErgonomicPenalty");

    [PatchPostfix]
    private static void Postfix(ref float __result)
    {
        if (__result >= 0f)
        {
            return;
        }

        SkillManager? skills = ClientAppUtils.GetClientApp()?.Session?.Profile?.Skills;
        if (skills is null)
        {
            return;
        }

        float reductionPercent = Plugin.GetBackpackErgonomicsReductionPercent(skills.Strength.Level);
        __result *= 1f - reductionPercent / 100f;
    }
}

internal sealed class MagazineRecoilPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.PropertyGetter(typeof(Mod), nameof(Mod.Recoil));

    [PatchPostfix]
    private static void Postfix(Mod __instance, ref float __result)
    {
        if (__instance is not Magazine || __result <= 0f)
        {
            return;
        }

        SkillManager? skills = ClientAppUtils.GetClientApp()?.Session?.Profile?.Skills;
        if (skills is null)
        {
            return;
        }

        float reductionPercent = Plugin.GetMagazineRecoilReductionPercent(skills.MagDrills.Level);
        __result *= 1f - reductionPercent / 100f;
    }
}

internal sealed class SkillEffectDisplayBuff : SkillManager.FloatBuff
{
    internal string Description { get; }

    internal SkillEffectDisplayBuff(string description, float value, EBuffId iconId)
    {
        Description = description;
        Value = value;
        Id = iconId;
        BuffType = SkillManager.EBuffType.Simple;
        HidenForPlayers = false;
    }
}

internal static class SkillEffectDisplayRegistry
{
    internal static SkillEffectDisplayBuff[] Create(Skill skill)
    {
        if (skill.Id == ESkillId.MagDrills)
        {
            return CreateVisible(new[]
            {
                CreateBuff("Reduces magazine ergonomics penalty",
                    Plugin.GetMagazineErgonomicsReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff),
                CreateBuff("Reduces magazine recoil penalty",
                    Plugin.GetMagazineRecoilReductionPercent(skill.Level), EBuffId.MagDrillsUnloadSpeed)
            });
        }

        if (Plugin.SkillsExtendedAvailable && skill.Id == ESkillId.SilentOps && !skill.Locked)
        {
            return CreateVisible(new[]
            {
                CreateBuff("Reduces suppressor ergonomics penalty",
                    Plugin.GetSuppressorErgonomicsReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff)
            });
        }

        if (skill.Id == ESkillId.AimDrills)
        {
            return CreateVisible(new[]
            {
                CreateBuff("Reduces sight ergonomics penalty",
                    Plugin.GetSightErgonomicsReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff),
                CreateBuff("Reduces headgear ergonomics penalty",
                    Plugin.GetHeadgearErgonomicsReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff)
            });
        }

        if (skill.Id == ESkillId.LightVests)
        {
            return CreateVisible(new[]
            {
                CreateBuff("Reduces light armor ergonomics penalty",
                    Plugin.GetLightArmorErgonomicsReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff)
            });
        }

        if (skill.Id == ESkillId.HeavyVests)
        {
            return CreateVisible(new[]
            {
                CreateBuff("Reduces heavy armor ergonomics penalty",
                    Plugin.GetHeavyArmorErgonomicsReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff)
            });
        }

        if (skill.Id == ESkillId.Endurance)
        {
            return CreateVisible(new[]
            {
                CreateBuff("Reduces backpack movement speed penalty",
                    Plugin.GetBackpackMovementReductionPercent(skill.Level), EBuffId.StrengthBuffSprintSpeedInc)
            });
        }

        if (skill.Id == ESkillId.Strength)
        {
            return CreateVisible(new[]
            {
                CreateBuff("Reduces backpack turn speed penalty",
                    Plugin.GetBackpackTurnReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff),
                CreateBuff("Reduces backpack ergonomics penalty",
                    Plugin.GetBackpackErgonomicsReductionPercent(skill.Level), EBuffId.WeaponErgonomicsBuff)
            });
        }

        return Array.Empty<SkillEffectDisplayBuff>();
    }

    private static SkillEffectDisplayBuff CreateBuff(string description, float value, EBuffId iconId) =>
        new(description, value, iconId);

    private static SkillEffectDisplayBuff[] CreateVisible(SkillEffectDisplayBuff[] buffs)
    {
        var visibleBuffs = new List<SkillEffectDisplayBuff>(buffs.Length);
        foreach (SkillEffectDisplayBuff buff in buffs)
        {
            if (buff.Value > 0f)
            {
                visibleBuffs.Add(buff);
            }
        }

        return visibleBuffs.ToArray();
    }
}

internal sealed class SkillPanelBuffsPatch : ModulePatch
{
    private static readonly FieldInfo IconTemplateField = AccessTools.Field(typeof(SkillPanel), "_buffIconTemplate");
    private static readonly FieldInfo IconContainerField = AccessTools.Field(typeof(SkillPanel), "_buffsContainer");
    private static readonly ConditionalWeakTable<SkillPanel, BuffIconGroup> IconGroups = new();

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(SkillPanel), nameof(SkillPanel.Show),
            new[] { typeof(Skill), typeof(EFT.HealthSystem.IHealthController) });

    [PatchPostfix]
    private static void Postfix(Skill skill, SkillPanel __instance)
    {
        SkillEffectDisplayBuff[] buffs = SkillEffectDisplayRegistry.Create(skill);
        if (buffs.Length == 0)
        {
            if (IconGroups.TryGetValue(__instance, out BuffIconGroup existingGroup))
            {
                existingGroup.HideAll();
            }
            return;
        }

        IconGroups.GetValue(__instance, CreateIconGroup).Show(skill, buffs);
    }

    private static BuffIconGroup CreateIconGroup(SkillPanel panel) =>
        new((BuffIcon)IconTemplateField.GetValue(panel), (Transform)IconContainerField.GetValue(panel));

    private sealed class BuffIconGroup
    {
        private readonly BuffIcon _template;
        private readonly Transform _container;
        private readonly List<BuffIcon> _icons = new();

        internal BuffIconGroup(BuffIcon template, Transform container)
        {
            _template = template;
            _container = container;
        }

        internal void Show(Skill skill, IReadOnlyList<SkillEffectDisplayBuff> buffs)
        {
            EnsureCount(buffs.Count);
            for (int index = 0; index < buffs.Count; index++)
            {
                _icons[index].Show(buffs[index], skill);
                _icons[index].gameObject.SetActive(true);
            }
            HideFrom(buffs.Count);
        }

        internal void HideAll() => HideFrom(0);

        private void EnsureCount(int count)
        {
            while (_icons.Count < count)
            {
                _icons.Add(UnityEngine.Object.Instantiate(_template, _container, false));
            }
        }

        private void HideFrom(int firstHiddenIndex)
        {
            for (int index = firstHiddenIndex; index < _icons.Count; index++)
            {
                _icons[index].gameObject.SetActive(false);
            }
        }
    }
}

internal sealed class SkillTooltipBuffsPatch : ModulePatch
{
    private static readonly FieldInfo PanelTemplateField = AccessTools.Field(typeof(SkillTooltip), "_buffPanelTemplate");
    private static readonly FieldInfo PanelContainerField = AccessTools.Field(typeof(SkillTooltip), "_buffContainer");
    private static readonly ConditionalWeakTable<SkillTooltip, BuffPanelGroup> PanelGroups = new();

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(SkillTooltip), nameof(SkillTooltip.Show), new[] { typeof(Skill) });

    [PatchPostfix]
    private static void Postfix(Skill skill, SkillTooltip __instance)
    {
        SkillEffectDisplayBuff[] buffs = SkillEffectDisplayRegistry.Create(skill);
        if (buffs.Length == 0)
        {
            if (PanelGroups.TryGetValue(__instance, out BuffPanelGroup existingGroup))
            {
                existingGroup.HideAll();
            }
            return;
        }

        PanelGroups.GetValue(__instance, CreatePanelGroup).Show(skill, buffs);
    }

    private static BuffPanelGroup CreatePanelGroup(SkillTooltip tooltip) =>
        new((BuffPanel)PanelTemplateField.GetValue(tooltip), (Transform)PanelContainerField.GetValue(tooltip));

    private sealed class BuffPanelGroup
    {
        private readonly BuffPanel _template;
        private readonly Transform _container;
        private readonly List<BuffPanel> _panels = new();

        internal BuffPanelGroup(BuffPanel template, Transform container)
        {
            _template = template;
            _container = container;
        }

        internal void Show(Skill skill, IReadOnlyList<SkillEffectDisplayBuff> buffs)
        {
            EnsureCount(buffs.Count);
            for (int index = 0; index < buffs.Count; index++)
            {
                BuffPanel panel = _panels[index];
                panel.Show(skill, buffs[index]);
                panel.UpdateBuff();
                panel.UpdateVisibility();
                panel._icon.gameObject.SetActive(true);
            }
            HideFrom(buffs.Count);
        }

        internal void HideAll() => HideFrom(0);

        private void EnsureCount(int count)
        {
            while (_panels.Count < count)
            {
                _panels.Add(UnityEngine.Object.Instantiate(_template, _container, false));
            }
        }

        private void HideFrom(int firstHiddenIndex)
        {
            for (int index = firstHiddenIndex; index < _panels.Count; index++)
            {
                _panels[index].gameObject.SetActive(false);
            }
        }
    }
}

internal sealed class BuffDescriptionPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BuffIcon), nameof(BuffIcon.GetBuffDescription),
            new[] { typeof(SkillManager.Buff) });

    [PatchPrefix]
    private static bool Prefix(SkillManager.Buff buff, ref string __result)
    {
        if (buff is not SkillEffectDisplayBuff displayBuff)
        {
            return true;
        }

        __result = $"{displayBuff.Description} by <color=#54c1ff>{displayBuff.Value:0.##}%</color>";
        return false;
    }
}
