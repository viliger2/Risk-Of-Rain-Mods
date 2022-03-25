﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
//using Aetherium;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using RoR2;
using UnityEngine.Networking;

namespace BubbetsItems.Items
{
    [HarmonyPatch]
    public class Hourglass : ItemBase
    {
        private MethodInfo _aetheriumOrig;
        private static Hourglass _instance;

        private ConfigEntry<string> buffBlacklist;

        private IEnumerable<BuffDef> buffDefBlacklist;
        //private static bool AetheriumEnabled => Chainloader.PluginInfos.ContainsKey(AetheriumPlugin.ModGuid);

        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            AddScalingFunction("[a] * 0.1 + 1.15", "Buff Duration");
            _instance = this;
        }

        // lmao i'm just going to abuse the timing of this because im lazy B)
        protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
        {
            var defaultValue = "bdBearVoidCooldown bdElementalRingsCooldown bdElementalRingVoidCooldown bdVoidFogMild bdVoidFogStrong bdVoidRaidCrabWardWipeFog";
            buffBlacklist = configFile.Bind(ConfigCategoriesEnum.General, "Hourglass Buff Blacklist", defaultValue, "Valid values: " +  string.Join(" ", BuffCatalog.nameToBuffIndex.Keys));
            buffBlacklist.SettingChanged += (_, _) => SettingChanged();
            SettingChanged();
        }
        
        private void SettingChanged()
        {
            buffDefBlacklist = from str in buffBlacklist.Value.Split(' ') select BuffCatalog.FindBuffIndex(str) into index where index != BuffIndex.None select BuffCatalog.GetBuffDef(index);
        }

        /*
        protected override void MakeBehaviours()
        {
            base.MakeBehaviours();
            if (!AetheriumEnabled) return;
            PatchAetherium();
        }
        
        private void PatchAetherium() // This needs to be its own function because for some reason typeof() was being called at the start of the function and it was throwing file not found exception
        {
            _aetheriumOrig = typeof(AetheriumPlugin).Assembly.GetType("Aetherium.Utils.ItemHelpers").GetMethod("RefreshTimedBuffs", new[] {typeof(CharacterBody), typeof(BuffDef), typeof(float), typeof(float)});
            Harmony.Patch(_aetheriumOrig, new HarmonyMethod(GetType().GetMethod("AetheriumTimedBuffHook")));
        }

        protected override void DestroyBehaviours()
        {
            base.DestroyBehaviours();
            if (!AetheriumEnabled) return;
            Harmony.Unpatch(_aetheriumOrig, HarmonyPatchType.Prefix);
        }*/

        protected override void MakeTokens()
        {
            AddToken("TIMED_BUFF_DURATION_ITEM_NAME", "Abundant Hourglass");
            AddToken("TIMED_BUFF_DURATION_ITEM_PICKUP", "Duration of " + "buffs ".Style(StyleEnum.Damage) + "are increased.");
            AddToken("TIMED_BUFF_DURATION_ITEM_DESC", "Duration of " + "buffs ".Style(StyleEnum.Damage) + "are multiplied by " + "{0}".Style(StyleEnum.Utility) + ".");
            AddToken("TIMED_BUFF_DURATION_ITEM_LORE", "BUB_TIMED_BUFF_DURATION_ITEM_LORE");
            base.MakeTokens();
        }
        
        // ReSharper disable once InconsistentNaming
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterBody), nameof(CharacterBody.AddTimedBuff), typeof(BuffDef), typeof(float))]
        public static void TimedBuffHook(CharacterBody __instance, BuffDef buffDef, ref float duration)
        {
            if (!NetworkServer.active) return;
            duration = DoDurationPatch(__instance, buffDef, duration);
        }
        
        // Hooked in awake
        public static void AetheriumTimedBuffHook(CharacterBody body, BuffDef buffDef, ref float taperStart)
        {
            taperStart = DoDurationPatch(body, buffDef, taperStart);
        }

        private static float DoDurationPatch(CharacterBody cb, BuffDef def, float duration)
        {
            if (def.isDebuff) return duration;
            if (_instance.buffDefBlacklist.Contains(def)) return duration;
            var inv = cb.inventory;
            if (!inv) return duration;
            var amount = cb.inventory.GetItemCount(_instance.ItemDef);
            if (amount <= 0) return duration;
            //scalingFunc.Parameters["a"] = amount;
            //var cont = new ExpressionC();
            duration *= _instance.scalingInfos[0].ScalingFunction(amount);
            //duration *= amount * 0.10f + 1.15f;
            //_instance.Logger.LogError(duration);
            return duration;
        }
    }
}