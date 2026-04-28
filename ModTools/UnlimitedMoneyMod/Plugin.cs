using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using System;

namespace UnlimitedMoneyMod
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public const string PLUGIN_GUID = "com.jorian.supermarket.unlimitedmoney";
        public const string PLUGIN_NAME = "Unlimited Money Mod";
        public const string PLUGIN_VERSION = "6.5.0";

        internal static new ManualLogSource Log;

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("[Mod] v6.5 -- Unlimited Money + Level 100 + MaxBoost + Toggles");
            Log.LogInfo("[Mod] Level -> 100 on load (auto)");
            Log.LogInfo("[Mod] Employee boost -> MAX permanently (auto)");
            Log.LogInfo("[Mod] F1 = Auto-Restock order (default: OFF)");
            Log.LogInfo("[Mod] F2 = Auto-Price + Auto-Buy Furniture (default: OFF)");
            Log.LogInfo("[Mod] F3 = Auto-Restock shelves (default: OFF)");
            Log.LogInfo("[Mod] F4 = Auto-Label new products (default: OFF)");

            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("[Mod] Harmony patches applied!");

            AutoRestockBehaviour.Register();
            AutoShopBehaviour.Register();
            KeyToggleBehaviour.Register();

            Log.LogInfo("[Mod] All systems GO!");
        }
    }

    public class KeyToggleBehaviour : MonoBehaviour
    {
        public KeyToggleBehaviour(IntPtr ptr) : base(ptr) { }

        private float _levelCheckTimer = 0f;
        private float _boostTimer = 0f;
        private const float BOOST_INTERVAL = 3f;

        void Update()
        {
            DeferredQueue.Process();
            PendingOrderSweeper.Tick();

            if (!LevelBooster.Done)
            {
                _levelCheckTimer += Time.deltaTime;
                if (_levelCheckTimer >= 3f)
                {
                    _levelCheckTimer = 0f;
                    LevelBooster.TryBoost("DelayedUpdate");
                }
            }

            _boostTimer += Time.deltaTime;
            if (_boostTimer >= BOOST_INTERVAL)
            {
                _boostTimer = 0f;
                BoostAllEmployees();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F1))
            {
                AutoRestockBehaviour.Enabled = !AutoRestockBehaviour.Enabled;
                Plugin.Log.LogInfo($"[Toggle] Auto-Restock: {(AutoRestockBehaviour.Enabled ? "ON" : "OFF")}");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F2))
            {
                AutoShopBehaviour.BuyAndPriceEnabled = !AutoShopBehaviour.BuyAndPriceEnabled;
                Plugin.Log.LogInfo($"[Toggle] Auto-Price + Auto-Buy Furniture: {(AutoShopBehaviour.BuyAndPriceEnabled ? "ON" : "OFF")}");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F3))
            {
                AutoShopBehaviour.RestockEnabled = !AutoShopBehaviour.RestockEnabled;
                Plugin.Log.LogInfo($"[Toggle] Auto-Restock shelves: {(AutoShopBehaviour.RestockEnabled ? "ON" : "OFF")}");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F4))
            {
                AutoShopBehaviour.AutoLabelEnabled = !AutoShopBehaviour.AutoLabelEnabled;
                Plugin.Log.LogInfo($"[Toggle] Auto-Label new products: {(AutoShopBehaviour.AutoLabelEnabled ? "ON" : "OFF")}");
            }
        }

        private void BoostAllEmployees()
        {
            try
            {
                var indicators = UnityEngine.Object.FindObjectsOfType<BoostIndicator>();
                if (indicators != null)
                {
                    for (int i = 0; i < indicators.Length; i++)
                    {
                        try
                        {
                            var bi = indicators[i];
                            if (bi == null) continue;
                            bi.AddBoost(100f);
                        }
                        catch {}
                    }
                }
            }
            catch {}
        }

        public static void Register()
        {
            ClassInjector.RegisterTypeInIl2Cpp<KeyToggleBehaviour>();

            var go = new GameObject("KeyToggleController");
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<KeyToggleBehaviour>();
        }
    }
}
