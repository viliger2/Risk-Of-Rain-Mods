﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using InLobbyConfig.Fields;
using NCalc;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Items;
using UnityEngine;
using Random = System.Random;

#nullable enable

namespace BubbetsItems
{
    [HarmonyPatch]
    public abstract class ItemBase : SharedBase
    {
        //protected virtual void MakeTokens(){} // Description is supposed to have % and + per item, pickup is a brief message about what the item does
        
        protected override void MakeConfigs()
        {
            var name = GetType().Name;
            Enabled = sharedInfo.ConfigFile.Bind("Disable Items", name, true, "Should this item be enabled.");
        }

        public ItemDef ItemDef;

        private static IEnumerable<ItemBase>? _items;
        public static IEnumerable<ItemBase> Items => _items ??= Instances.OfType<ItemBase>();

        public List<ScalingInfo> scalingInfos = new();
        public VoidPairing? voidPairing;
        protected string SimpleDescriptionToken;

        protected void AddScalingFunction(string defaultValue, string name, string? desc = null, string? oldDefault = null)
        {
            scalingInfos.Add(new ScalingInfo(sharedInfo.ConfigFile, defaultValue, name, new StackFrame(1).GetMethod().DeclaringType, desc, oldDefault));
        }

        private static Regex formatArgParams = new Regex(@"({\d:?.*?})+", RegexOptions.Compiled);
        public override string GetFormattedDescription(Inventory? inventory, string? token = null, bool forceHideExtended = false)
        {
            // ReSharper disable twice Unity.NoNullPropagation

            if (scalingInfos.Count <= 0) return Language.GetString(ItemDef.descriptionToken);

            var formatArgs = scalingInfos.Select(info => info.ScalingFunction(inventory?.GetItemCount(ItemDef)))
                .Cast<object>().ToArray();
            
            if (sharedInfo.UseSimpleDescIfApplicable.Value && scalingInfos.All(x => x.IsDefault) && !string.IsNullOrEmpty(SimpleDescriptionToken))
            {
                var ret = Language.GetString(sharedInfo.TokenPrefix + SimpleDescriptionToken);
                if (sharedInfo.ItemStatsInSimpleDesc.Value && !forceHideExtended)
                {
                    var para = new List<string>();

                    ret += "\n\n";
                    
                    // Holy fuck i hate regex in c#
                    foreach (Capture matchGroupCapture in formatArgParams.Match(Language.GetString(token ?? ItemDef.descriptionToken)).Groups[0].Captures)
                    {
                        var val = matchGroupCapture.Value;
                        if (!string.IsNullOrEmpty(val))
                            para.Add(val);
                    }

                    foreach (var param in para)
                    {
                        if (int.TryParse(param[1].ToString(), out var i))
                            ret += "\n" + scalingInfos[i]._name + ": " + param;
                    }

                    ret = string.Format(ret, formatArgs);
                }

                return ret;
            }
            else
            {
                var ret = Language.GetStringFormatted(token ?? ItemDef.descriptionToken, formatArgs);
                if (sharedInfo.ExpandedTooltips.Value && !forceHideExtended)
                    ret += "\n\nTooltip updates automatically with these fully configurable Scaling Functions:\n" +
                           string.Join("\n", scalingInfos.Select(info => info.ToString()));
                return ret;
            }
        }

        public override void MakeInLobbyConfig(Dictionary<ConfigCategoriesEnum, List<object>> scalingFunctions)
        {
            base.MakeInLobbyConfig(scalingFunctions);
            foreach (var info in scalingInfos)
            {
                info.MakeInLobbyConfig(scalingFunctions[ConfigCategoriesEnum.BalancingFunctions]);
            }
        }
        
        public override void MakeRiskOfOptions()
        {
            base.MakeRiskOfOptions();
            foreach (var info in scalingInfos)
            {
                info.MakeRiskOfOptions();
            }
        }

        protected override void FillDefsFromSerializableCP(SerializableContentPack serializableContentPack)
        {
            base.FillDefsFromSerializableCP(serializableContentPack);
            var name = GetType().Name;
            foreach (var itemDef in serializableContentPack.itemDefs)
            {
                if (MatchName(itemDef.name, name)) ItemDef = itemDef;
            }
            if (ItemDef == null)
            {
                sharedInfo.Logger?.LogWarning($"Could not find ItemDef for item {this} in serializableContentPack, class/itemdef name are probably mismatched. This will throw an exception later.");
            }
        }
        
        public override void AddDisplayRules(VanillaIDRS which, ItemDisplayRule[] displayRules)
        {
            var rules = IDRHelper.GetRuleSet(which).keyAssetRuleGroups;
            rules = rules.AddItem(new ItemDisplayRuleSet.KeyAssetRuleGroup
            {
                displayRuleGroup = new DisplayRuleGroup {rules = displayRules},
                keyAsset = ItemDef
            }).ToArray();
            IDRHelper.GetRuleSet(which).keyAssetRuleGroups = rules;
        }
        public override void AddDisplayRules(ModdedIDRS which, ItemDisplayRule[] displayRules)
        {
            var rules = IDRHelper.GetRuleSet(which).keyAssetRuleGroups;
            rules = rules.AddItem(new ItemDisplayRuleSet.KeyAssetRuleGroup
            {
                displayRuleGroup = new DisplayRuleGroup {rules = displayRules},
                keyAsset = ItemDef
            }).ToArray();
            IDRHelper.GetRuleSet(which).keyAssetRuleGroups = rules;
        }
        

        protected override void FillDefsFromContentPack()
        {
            foreach (var pack in ContentPacks)
            {
                if (ItemDef != null) continue;
                var name = GetType().Name;
                foreach (var itemDef in pack.itemDefs)
                    if (MatchName(itemDef.name, name))
                        ItemDef = itemDef;
            }

            if (ItemDef == null) 
                sharedInfo.Logger?.LogWarning(
                    $"Could not find ItemDef for item {this}, class/itemdef name are probably mismatched. This will throw an exception later.");
        }

        protected override void FillPickupIndex()
        {
            try
            {
                var pickup = PickupCatalog.FindPickupIndex(ItemDef.itemIndex);
                PickupIndex = pickup;
                PickupIndexes.Add(pickup, this);
            }
            catch (NullReferenceException e)
            {
                sharedInfo.Logger?.LogError("Equipment " + GetType().Name +
                                            " threw a NRE when filling pickup indexes, this could mean its not defined in your content pack:\n" +
                                            e);
            }
        }

        protected override void FillRequiredExpansions()
        {
            if (RequiresSotv)
                ItemDef.requiredExpansion = SotvExpansion;
            else
                ItemDef.requiredExpansion = BubExpansion;
        }
        
        [HarmonyPrefix, HarmonyPatch(typeof(ContagiousItemManager), nameof(ContagiousItemManager.Init))]
        public static void FillVoidItems()
        {
            var pairs = new List<ItemDef.Pair>();
            foreach (var itemBase in Items)
            {
                itemBase.FillVoidConversions(pairs);
            }

            ItemCatalog.itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem] = ItemCatalog
                .itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem].AddRangeToArray(pairs.ToArray());
        }

        protected virtual void FillVoidConversions(List<ItemDef.Pair> pairs) {}


        public static void CheatForAllItems()
        {
            foreach (var itemBase in Items)
            {
                itemBase.CheatForItem(UnityEngine.Random.onUnitSphere);
            }
        }
        
        public class ScalingInfo
        {
            private readonly string _description;
            private readonly ConfigEntry<string> _configEntry;
            private Func<ExpressionContext, float> _function;
            private string _oldValue;
            public readonly string _name;
            private readonly ExpressionContext _defaultContext;
            public readonly ExpressionContext WorkingContext;
            private string _defaultValue;

            public string Value
            {
                get => _configEntry.Value;
                set => _configEntry.Value = value;
            }

            public bool IsDefault => _configEntry.Value == _defaultValue;

            public ScalingInfo(ConfigFile configFile, string defaultValue, string name, Type callingType, string? desc = null, string? oldDefault = null)
            {
                _description = desc ?? "[a] = item count";
                _name = name;
                WorkingContext = new ExpressionContext();

                _configEntry = configFile.Bind(ConfigCategoriesEnum.BalancingFunctions, callingType.Name + "_" + name, defaultValue,   callingType.Name + "; Scaling function for item. ;" + _description, oldDefault);
                _defaultValue = defaultValue;
                _oldValue = _configEntry.Value;
                _function = new Expression(_oldValue).ToLambda<ExpressionContext, float>();
                _configEntry.SettingChanged += EntryChanged;
            }

            public float ScalingFunction(ExpressionContext? context = null)
            {
                return _function(context ?? _defaultContext);
            }
            public float ScalingFunction(int? itemCount)
            {
                WorkingContext.a = itemCount ?? 1;
                return ScalingFunction(WorkingContext);
            }

            public override string ToString()
            {
                return _oldValue + "\n(" + _name + ": " + _description + ")";
            }

            public void MakeInLobbyConfig(List<object> modConfigEntryObj)
            {
                modConfigEntryObj.Add(ConfigFieldUtilities.CreateFromBepInExConfigEntry(_configEntry));
            }

            public void MakeRiskOfOptions()
            {
                ModSettingsManager.AddOption(new StringInputFieldOption(_configEntry));
            }

            private void EntryChanged(object sender, EventArgs e)
            {
                if (_configEntry.Value == _oldValue) return;
                try
                {
                    _function = new Expression(_configEntry.Value).ToLambda<ExpressionContext, float>();
                    _oldValue = _configEntry.Value;
                } catch(EvaluationException){}
            }
        }

        public class VoidPairing
        {
            public static string ValidEntries = string.Join(" ", ItemCatalog.itemNames);
            private ConfigEntry<string> configEntry;
            private ItemBase Parent;

            public VoidPairing(string defaultValue, ItemBase parent, string? oldDefault = null)
            {
                Parent = parent;
                configEntry = parent.sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Void Conversions: " + parent.GetType().Name, defaultValue, "Valid values: " + ValidEntries, oldDefault);
                configEntry.SettingChanged += (_, _) => SettingChanged();
                SettingChanged();
            }

            private void SettingChanged()
            {
                var pairs = ItemCatalog.itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem].Where(x => x.itemDef2 != Parent.ItemDef);
                var newPairs = from str in configEntry.Value.Split(' ') select ItemCatalog.FindItemIndex(str) into index where index != ItemIndex.None select new ItemDef.Pair {itemDef1 = ItemCatalog.GetItemDef(index), itemDef2 = Parent.ItemDef};
                ItemCatalog.itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem] = pairs.Union(newPairs).ToArray();
            }
        }
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public class ExpressionContext
        {
            // yes this is terrible but im not smart enough to figure out another way.
            public float a;
            public float b;
            public float c;
            public float d;
            public float e;
            public float f;
            public float g;
            public float h;
            public float i;
            public float j;
            public float k;
            public float l;
            public float m;
            public float n;
            public float o;
            public float p;
            public float q;
            public float r;
            public float s;
            public float t;
            public float u;
            public float v;
            public float w;
            public float x;
            public float y;
            public float z;

            public int RoundToInt(float x)
            {
                return Mathf.RoundToInt(x);
            }

            public float Log(float x)
            {
                return Mathf.Log(x);
            }

            public float Pow(float x, float y)
            {
                return Mathf.Pow(x, y);
            }

            public float Sin(float x)
            {
                return Mathf.Sin(x);
            }
            
            public float Tan(float x)
            {
                return Mathf.Tan(x);
            }

            public float Max(float x, float y)
            {
                return Mathf.Max(x, y);
            }

            public float Min(float x, float y)
            {
                return Mathf.Min(x, y);
            }
        }

        public void AddVoidPairing(string defaultValue)
        {
            voidPairing = new VoidPairing(defaultValue, this);
        }
    }
}