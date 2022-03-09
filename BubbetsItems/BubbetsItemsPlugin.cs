﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
//using Aetherium; TODO readd support
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;
using Path = System.IO.Path;
using SearchableAttribute = HG.Reflection.SearchableAttribute;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
[module: UnverifiableCode]
[assembly: SearchableAttribute.OptIn]
namespace BubbetsItems
{
    [BepInPlugin("bubbet.bubbetsitems", "Bubbets Items", "1.4.3")]
    //[BepInDependency(R2API.R2API.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]//, R2API.Utils.R2APISubmoduleDependency(nameof(R2API.RecalculateStatsAPI))]
    //[BepInDependency(AetheriumPlugin.ModGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.KingEnderBrine.InLobbyConfig", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.xoxfaby.BetterUI", BepInDependency.DependencyFlags.SoftDependency)]
    public class BubbetsItemsPlugin : BaseUnityPlugin
    {
        private const string AssetBundleName = "MainAssetBundle";
        
        public static ContentPack ContentPack;
        public static AssetBundle AssetBundle;
        public List<SharedBase> forwardTest => SharedBase.Instances;
        public static BubbetsItemsPlugin instance;
        public static ManualLogSource Log;

        public void Awake()
        {
            instance = this;
            Log = Logger;
            RoR2Application.isModded = true;
            Conf.Init(Config);
            var harm = new Harmony(Info.Metadata.GUID);
            LoadContentPack(harm);
            InLobbyConfigCompat.Init();
            harm.PatchAll();
            //Fucking bepinex pack constantly changing and now loading too late for searchableAttributes scan.
            SearchableAttribute.ScanAssembly(Assembly.GetExecutingAssembly());

            //PickupTooltipFormat.Init(harm);
        }

        private static uint _bankID;

        [SystemInitializer]
        public static void LoadSoundBank()
        {
            try
            {
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                /*
                Stream stream = File.Open(Path.Combine(path, "BubbetsItems.bnk"), FileMode.Open);
                var array = new byte[stream.Length];
                stream.Read(array, 0, array.Length);

                var mem = Marshal.AllocHGlobal(array.Length);
                Marshal.Copy(array, 0, mem, array.Length);
                var result = AkSoundEngine.LoadBank(mem, (uint) array.Length, out _bankID);*/
                AkSoundEngine.AddBasePath(path);
                var result = AkSoundEngine.LoadBank("BubbetsItems.bnk", out _bankID);
                //var result = AkSoundEngine.LoadBank("BubbetsItems.bnk", out _bankID);
                if (result != AKRESULT.AK_Success)
                    Debug.LogError("[Bubbets Items] SoundBank Load Failed: " + result);
            }
            catch (Exception e)
            {
                Log.LogError(e);
            }
        }

        [SystemInitializer]
        public static void ExtraTokens()
        {
            Language.english.SetStringByToken("BUB_HOLD_TOOLTIP", "Hold Capslock for more.");
        }

        public static class Conf
        {
            public static ConfigEntry<bool> AmmoPickupAsOrbEnabled;
            //public static bool RequiresR2Api;

            internal static void Init(ConfigFile configFile)
            {
                AmmoPickupAsOrbEnabled = configFile.Bind("Disable Mod Parts", "Ammo Pickup As Orb", true,  "Should the Ammo Pickup as an orb be enabled.");
            }
        }

        private void LoadContentPack(Harmony harmony)
        {
            var path = Path.GetDirectoryName(Info.Location);
            AssetBundle = AssetBundle.LoadFromFile(Path.Combine(path, AssetBundleName));
            var serialContent = AssetBundle.LoadAsset<BubsItemsContentPackProvider>("MainContentPack");
            
            SharedBase.Initialize(Logger, Config, serialContent, harmony, "BUB_");
            ContentPack = serialContent.CreateContentPack();
            SharedBase.AddContentPack(ContentPack);
            ContentPackProvider.Initialize(Info.Metadata.GUID, ContentPack);

            if (!Conf.AmmoPickupAsOrbEnabled.Value) return;
            var go = AssetBundle.LoadAsset<GameObject>("AmmoPickupOrb");
            //go.AddComponent<HarmonyPatches.AmmoPickupOrbBehavior>(); // Doing this at runtime to avoid reimporting component to unity
            ContentPack.effectDefs.Add(new[] {new EffectDef(go)});
        }

        private class ContentPackProvider : IContentPackProvider
        {
            private static ContentPack contentPack;
            private static string _identifier;
            public string identifier => _identifier;

            public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
            {
                //ContentPack.identifier = identifier;
                args.ReportProgress(1f);
                yield break;
            }

            public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
            {
                ContentPack.Copy(contentPack, args.output);
                //Log.LogError(ContentPack.identifier);
                args.ReportProgress(1f);
                yield break;
            }

            public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
            {
                args.ReportProgress(1f);
                Log.LogInfo("Contentpack finished");
                yield break;
            }

            internal static void Initialize(string identifier, ContentPack pack)
            {
                _identifier = identifier;
                contentPack = pack;
                ContentManager.collectContentPackProviders += AddCustomContent;
            }

            private static void AddCustomContent(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
            {
                addContentPackProvider(new ContentPackProvider());
            }
        }
    }
}