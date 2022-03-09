﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BubbetsItems.Helpers;
using HarmonyLib;
using RoR2;
using RoR2.Items;
using UnityEngine;

namespace BubbetsItems.Items
{
	public class VoidJunkToScrapTier1 : ItemBase
	{
		private static VoidJunkToScrapTier1 instance;
		public override bool RequiresSOTV { get; protected set; } = true;

		protected override void MakeTokens()
		{
			base.MakeTokens();
			AddToken("VOIDJUNKTOSCRAPTIER1_NAME", "Void Scrap");
			AddToken("VOIDJUNKTOSCRAPTIER1_PICKUP", $"{"Corrupts all broken items".Style(StyleEnum.Void)} into scrap.");
			AddToken("VOIDJUNKTOSCRAPTIER1_DESC", $"{"Corrupts all broken items".Style(StyleEnum.Void)} and converts them into usable {"White scrap".Style(StyleEnum.White)}.");
			AddToken("VOIDJUNKTOSCRAPTIER1_LORE", "");
			instance = this;
		}

		[HarmonyPostfix, HarmonyPatch(typeof(CostTypeCatalog), nameof(CostTypeCatalog.Init))]
		public static void FixBuying()
		{
			try
			{
				var def = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.WhiteItem);
				var oldCan = def.isAffordable;
				def.isAffordable = (typeDef, context) =>
				{
					if (oldCan(typeDef, context)) return true;
					try
					{
						if (typeDef.itemTier != ItemTier.Tier1) return false;
						var inv = context.activator.GetComponent<CharacterBody>().inventory;
						var voidAmount = Math.Max(0, inv.GetItemCount(instance.ItemDef) - 1);
						return inv.GetTotalItemCountOfTier(ItemTier.Tier1) + voidAmount >= context.cost;
					}
					catch (Exception e)
					{
						BubbetsItemsPlugin.Log.LogError(e);
						return false;
					}
				};
				var oldCost = def.payCost;
				def.payCost = (typeDef, context) =>
				{
					if (typeDef.itemTier != ItemTier.Tier1)
					{
						oldCost(typeDef, context);
						return;
					}

					try
					{
						var inv = context.activatorBody.inventory;

						var highestPriority = new WeightedSelection<ItemIndex>();
						var higherPriority = new WeightedSelection<ItemIndex>();
						var highPriority = new WeightedSelection<ItemIndex>();
						var normalPriority = new WeightedSelection<ItemIndex>();
						
						var voidAmount = Math.Max(0, inv.GetItemCount(instance.ItemDef) - 1);
						if (voidAmount > 0)
							highestPriority.AddChoice(instance.ItemDef.itemIndex, voidAmount);
						
						foreach (var itemIndex in ItemCatalog.tier1ItemList)
						{
							if (itemIndex == context.avoidedItemIndex) continue;
							var count = inv.GetItemCount(itemIndex);
							if (count > 0)
							{
								var itemDef = ItemCatalog.GetItemDef(itemIndex);
								(itemDef.ContainsTag(ItemTag.PriorityScrap) ? higherPriority : itemDef.ContainsTag(ItemTag.Scrap) ? highPriority : normalPriority).AddChoice(itemIndex, count);
							}
						}

						var itemsToTake = new List<ItemIndex>();

						TakeFromWeightedSelection(highestPriority, ref context, ref itemsToTake);
						TakeFromWeightedSelection(higherPriority, ref context, ref itemsToTake);
						TakeFromWeightedSelection(highPriority, ref context, ref itemsToTake);
						TakeFromWeightedSelection(normalPriority, ref context, ref itemsToTake);

						for (var i = itemsToTake.Count; i < context.cost; i++)
							itemsToTake.Add(context.avoidedItemIndex);

						context.results.itemsTaken = itemsToTake;
						foreach (var itemIndex in itemsToTake) inv.RemoveItem(itemIndex);
						MultiShopCardUtils.OnNonMoneyPurchase(context);
					}
					catch (Exception e)
					{
						BubbetsItemsPlugin.Log.LogError(e);
					}
				};
			}
			catch (Exception e)
			{
				BubbetsItemsPlugin.Log.LogError(e);
			}
		}

		private static void TakeFromWeightedSelection(WeightedSelection<ItemIndex> weightedSelection, ref CostTypeDef.PayCostContext context, ref List<ItemIndex> itemsToTake)
		{
			while (weightedSelection.Count > 0 && itemsToTake.Count < context.cost)
			{
				var choiceIndex = weightedSelection.EvaluateToChoiceIndex(context.rng.nextNormalizedFloat);
				var choice = weightedSelection.GetChoice(choiceIndex);
				var value = choice.value;
				var num = (int)choice.weight;
				num--;
				if (num <= 0)
				{
					weightedSelection.RemoveChoice(choiceIndex);
				}
				else
				{
					weightedSelection.ModifyChoiceWeight(choiceIndex, num);
				}
				itemsToTake.Add(value);
			}
		}


		protected override void FillVoidConversions()
		{
			ItemCatalog.itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem] = ItemCatalog.itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem].AddRangeToArray(new []{new ItemDef.Pair
				{
					itemDef1 = DLC1Content.Items.FragileDamageBonusConsumed, 
					itemDef2 = ItemDef
				},
				new ItemDef.Pair
				{
					itemDef1 = DLC1Content.Items.HealingPotionConsumed,
					itemDef2 = ItemDef
				},
				new ItemDef.Pair
				{
					itemDef1 = DLC1Content.Items.ExtraLifeVoidConsumed,
					itemDef2 = ItemDef
				},
				new ItemDef.Pair
				{
					itemDef1 = RoR2Content.Items.ExtraLifeConsumed,
					itemDef2 = ItemDef
				}
			});
		}
	}
}