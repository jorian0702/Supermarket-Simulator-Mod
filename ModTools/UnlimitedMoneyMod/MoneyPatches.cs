using HarmonyLib;
using System;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace UnlimitedMoneyMod
{
    [HarmonyPatch(typeof(MoneyManager), nameof(MoneyManager.MoneyTransition))]
    public static class MoneyTransitionPatch
    {
        static void Prefix(ref float amount, MoneyManager.TransitionType type)
        {
            if (amount < 0)
            {
                Plugin.Log.LogInfo($"[UnlimitedMoney] Blocked spending: {amount:F2} ({type}) -> 0");
                amount = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(MoneyManager), nameof(MoneyManager.HasMoney))]
    public static class HasMoneyPatch
    {
        static void Postfix(ref bool __result)
        {
            __result = true;
        }
    }

    [HarmonyPatch(typeof(PlayerPurchaser), nameof(PlayerPurchaser.Spend))]
    public static class PlayerSpendPatch
    {
        static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerPurchaser), nameof(PlayerPurchaser.Money), MethodType.Getter)]
    public static class PlayerMoneyGetterPatch
    {
        static void Postfix(ref float __result)
        {
            if (__result < 99999999f)
                __result = 99999999f;
        }
    }

    [HarmonyPatch(typeof(MoneyManager), nameof(MoneyManager.Money), MethodType.Getter)]
    public static class MoneyManagerMoneyGetterPatch
    {
        static void Postfix(MoneyManager __instance, ref float __result)
        {
            if (__result < 99999999f)
            {
                __instance.Money = 99999999f;
                __result = 99999999f;
            }
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.ApplySaveData))]
    public static class LevelBoostOnLoad
    {
        static void Postfix()
        {
            LevelBooster.Done = false;
            LevelBooster.TryBoost("ApplySaveData");
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.CreateLoadNewSave))]
    public static class LevelBoostOnNewGame
    {
        static void Postfix()
        {
            LevelBooster.Done = false;
            LevelBooster.TryBoost("CreateLoadNewSave");
        }
    }

    public static class LevelBooster
    {
        public const int TARGET_LEVEL = 100;
        public static bool Done { get; set; } = false;

        public static void TryBoost(string source)
        {
            try
            {
                var slm = StoreLevelManager.Instance;
                if (slm == null)
                {
                    Plugin.Log.LogWarning($"[LevelBoost] StoreLevelManager is null ({source})");
                    return;
                }

                int current = slm.CurrentLevel;
                if (current >= TARGET_LEVEL)
                {
                    Done = true;
                    return;
                }

                slm.CurrentLevel = TARGET_LEVEL;
                slm.CurrentPoint = 0;
                slm.RefreshLevel();
                Done = true;
                Plugin.Log.LogInfo($"[LevelBoost] Level set: {current} -> {TARGET_LEVEL} (via {source})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[LevelBoost] Failed ({source}): {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.HasAvailableCashierSlot))]
    public static class CashierSlotPatch
    {
        static void Postfix(ref bool __result) { __result = true; }
    }

    public static class HiringUnlocker
    {
        public static void UnlockAll()
        {
            try
            {
                var cm = CheckoutManager.Instance;
                if (cm != null)
                    cm.completedCustomerCount = 99999;

                var allCashierSO = Resources.FindObjectsOfTypeAll<CashierSO>();
                int count = 0;
                if (allCashierSO != null)
                {
                    for (int i = 0; i < allCashierSO.Length; i++)
                    {
                        var cso = allCashierSO[i];
                        if (cso == null) continue;
                        cso.CheckoutGoalToUnlock = 0;
                        cso.RequiredStoreLevel = 0;
                        count++;
                        Plugin.Log.LogInfo($"[HiringUnlock] Unlocked {cso.CashierName} (ID:{cso.ID}, goal:0, level:0)");
                    }
                }
                Plugin.Log.LogInfo($"[HiringUnlock] Done: {count} cashiers unlocked, completedCount=99999");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[HiringUnlock] Failed: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(CashierItem), nameof(CashierItem.Setup))]
    public static class CashierItemSetupPatch
    {
        static void Prefix()
        {
            HiringUnlocker.UnlockAll();
        }
    }

    [HarmonyPatch(typeof(CashierItem), nameof(CashierItem.OnEnable))]
    public static class CashierItemOnEnablePatch
    {
        static void Prefix()
        {
            HiringUnlocker.UnlockAll();
        }
    }

    // --- Suppress noisy Unity warnings ---
    // Combines clone-induced spam (pool despawn, TMP CullMode) and scene-level
    // warnings the game devs left in (mirrored buildings, dup LODGroups). All harmless,
    // pure log noise. Original warnings unrelated to these patterns pass through.
    public static class NoisyWarningFilter
    {
        public static readonly string[] Blocked = new[]
        {
            "wasn't spawned from a pool",
            "doesn't have a float or range property '_CullMode'",
            "BoxCollider does not support negative scale",
            "effective box size has been forced positive",
            "If you absolutely need to use negative scaling",
            "is registered with more than one LODGroup",
        };

        public static bool ShouldBlock(Il2CppSystem.Object message)
        {
            try
            {
                var s = message?.ToString();
                if (s == null) return false;
                for (int i = 0; i < Blocked.Length; i++)
                    if (s.Contains(Blocked[i])) return true;
            }
            catch {}
            return false;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogWarning), new System.Type[] { typeof(Il2CppSystem.Object) })]
    public static class SuppressLogWarningPatch
    {
        static bool Prefix(Il2CppSystem.Object message) => !NoisyWarningFilter.ShouldBlock(message);
    }

    [HarmonyPatch(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogWarning), new System.Type[] { typeof(Il2CppSystem.Object), typeof(UnityEngine.Object) })]
    public static class SuppressLogWarningContextPatch
    {
        static bool Prefix(Il2CppSystem.Object message) => !NoisyWarningFilter.ShouldBlock(message);
    }

    [HarmonyPatch(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.Log), new System.Type[] { typeof(Il2CppSystem.Object) })]
    public static class SuppressLogPatch
    {
        static bool Prefix(Il2CppSystem.Object message) => !NoisyWarningFilter.ShouldBlock(message);
    }

    [HarmonyPatch(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogError), new System.Type[] { typeof(Il2CppSystem.Object) })]
    public static class SuppressLogErrorPatch
    {
        static bool Prefix(Il2CppSystem.Object message) => !NoisyWarningFilter.ShouldBlock(message);
    }

    [HarmonyPatch(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogError), new System.Type[] { typeof(Il2CppSystem.Object), typeof(UnityEngine.Object) })]
    public static class SuppressLogErrorContextPatch
    {
        static bool Prefix(Il2CppSystem.Object message) => !NoisyWarningFilter.ShouldBlock(message);
    }

    // Deferred-action queue: actions enqueued from inside Harmony patches run on the
    // next Update tick. Used to delay m_ActiveRestockers.Add until after the game's
    // own LoadData iteration finishes, to avoid InvalidOperationException.
    public static class DeferredQueue
    {
        static readonly System.Collections.Generic.List<System.Action> _pending = new();
        public static void Enqueue(System.Action a) { lock (_pending) _pending.Add(a); }
        public static void Process()
        {
            System.Collections.Generic.List<System.Action> snap;
            lock (_pending) { if (_pending.Count == 0) return; snap = new(_pending); _pending.Clear(); }
            foreach (var a in snap) { try { a?.Invoke(); } catch (Exception e) { Plugin.Log.LogWarning($"[DeferredQueue] action failed: {e.Message}"); } }
        }
    }

    // --- GetSpawnPosition guard ---
    // Clones in m_ActiveRestockers get their internal ID from list-position (or similar),
    // which exceeds m_RestockerSpawnPositions.Length (6). Without this guard the game
    // throws IndexOutOfRangeException in ClerkIdleState.OnTick every frame per clone.
    [HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.GetSpawnPosition))]
    public static class GetSpawnPositionGuardPatch
    {
        static int _logCount = 0;
        static bool Prefix(int restockerID, ref Transform __result, EmployeeManager __instance)
        {
            try
            {
                var positions = __instance.m_RestockerSpawnPositions;
                int len = positions?.Length ?? 0;
                if (positions == null || len == 0) return true;
                // Game appears to use positions[id-1] (1-indexed input). Original IDs 1..6
                // are safe; anything outside is invalid and triggers IndexOutOfRange.
                if (restockerID < 1 || restockerID > len)
                {
                    int safe = ((restockerID - 1) % len + len) % len;
                    __result = positions[safe];
                    if (_logCount < 10)
                    {
                        Plugin.Log.LogInfo($"[GetSpawnPos] guard: id={restockerID} -> slot {safe} (len={len})");
                        _logCount++;
                    }
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[GetSpawnPos] guard err id={restockerID}: {e.Message}");
                return true;
            }
        }
    }

    // --- Double Restocker Models per Hire ---
    // Save-safe: each hired ID still has only ONE entry in m_RestockersData (the only
    // thing the game persists). We piggyback on the runtime spawn — for every Clerk
    // the game spawns, we create a clone GameObject and inject it into m_ActiveRestockers
    // so the dispatcher treats it as a real worker. Both Clerks share the same EmployeeId,
    // which is fine because job-collision is resolved at the per-product HashSet layer
    // (m_OccupiedProductsByRestockers), not by ID uniqueness.

    [HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.SpawnRestocker))]
    public static class DoubleRestockerSpawnPatch
    {
        const string CLONE_NAME_PREFIX = "ClerkClone_";

        static void Postfix(int restockerID, EmployeeManager __instance)
        {
            try
            {
                var origClerk = __instance.GetRestockerByID(restockerID);
                if (origClerk == null || origClerk.gameObject == null)
                {
                    Plugin.Log.LogWarning($"[DoubleSpawn] origClerk null for id={restockerID}");
                    return;
                }

                var pos = origClerk.transform.position;
                var rot = origClerk.transform.rotation;
                var clone = UnityEngine.Object.Instantiate(origClerk.gameObject, pos, rot);
                clone.name = $"{CLONE_NAME_PREFIX}{restockerID}";

                var cloneClerk = clone.GetComponent<SupermarketSimulator.Clerk.Clerk>();
                if (cloneClerk == null)
                {
                    Plugin.Log.LogWarning($"[DoubleSpawn] clone has no Clerk component for id={restockerID}");
                    UnityEngine.Object.Destroy(clone);
                    return;
                }

                // Explicitly copy IDs — Unity Instantiate may not preserve runtime-assigned values.
                int origEmpId = -999, cloneEmpId = -999, origRstId = -999, cloneRstId = -999;
                try { origEmpId = origClerk.EmployeeId; cloneClerk.EmployeeId = origEmpId; cloneEmpId = cloneClerk.EmployeeId; } catch (Exception ex) { Plugin.Log.LogWarning($"[DoubleSpawn] EmployeeId copy fail: {ex.Message}"); }

                var origRestocker = origClerk.gameObject.GetComponent<Restocker>();
                var cloneRestocker = clone.GetComponent<Restocker>();
                if (origRestocker != null && cloneRestocker != null)
                {
                    try { origRstId = origRestocker.RestockerID; cloneRestocker.RestockerID = origRstId; cloneRstId = cloneRestocker.RestockerID; } catch (Exception ex) { Plugin.Log.LogWarning($"[DoubleSpawn] RestockerID copy fail: {ex.Message}"); }
                }

                var nav = clone.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null)
                {
                    nav.enabled = false;
                    nav.Warp(pos);
                    nav.enabled = true;
                }

                // Defer list mutation + load-done callback to next Update frame so we don't
                // modify m_ActiveRestockers while the game is iterating it during LoadData.
                int capturedId = restockerID;
                var capturedClerk = cloneClerk;
                var capturedRestocker = cloneRestocker;
                var capturedMgr = __instance;
                DeferredQueue.Enqueue(() =>
                {
                    var list = capturedMgr.m_ActiveRestockers;
                    if (list != null) list.Add(capturedClerk);
                    if (capturedRestocker != null)
                    {
                        try { capturedRestocker.OnEmployeeManagerLoadDone(); }
                        catch (Exception re) { Plugin.Log.LogWarning($"[DoubleSpawn] OnLoadDone failed id={capturedId}: {re.Message}"); }
                    }
                    Plugin.Log.LogInfo($"[DoubleSpawn] id={capturedId} activated (active={list?.Count ?? -1})");
                });

                Plugin.Log.LogInfo($"[DoubleSpawn] id={restockerID} cloned | EmpId orig={origEmpId} clone={cloneEmpId} | RstId orig={origRstId} clone={cloneRstId}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[DoubleSpawn] failed for id={restockerID}: {e.Message}"); }
        }
    }

    // --- Employee Speed Override Patches ---

    [HarmonyPatch(typeof(SupermarketSimulator.Clerk.Clerk), nameof(SupermarketSimulator.Clerk.Clerk.PlacingProductsInterval), MethodType.Getter)]
    public static class ClerkSpeedPatch
    {
        static void Postfix(ref float __result) { __result = 0.05f; }
    }

    [HarmonyPatch(typeof(Cashier), nameof(Cashier.CurrentScanSpeed), MethodType.Getter)]
    public static class CashierSpeedPatch
    {
        static void Postfix(ref float __result) { if (__result < 5f) __result = 5f; }
    }

    [HarmonyPatch(typeof(CustomerHelper), nameof(CustomerHelper.CurrentScanSpeed), MethodType.Getter)]
    public static class CustomerHelperSpeedPatch
    {
        static void Postfix(ref float __result) { if (__result < 5f) __result = 5f; }
    }

    // --- Employee Max Boost Patches (visual + AddBoost fallback) ---

    [HarmonyPatch(typeof(BoostIndicator), nameof(BoostIndicator.GetBoostAmount))]
    public static class MaxBoostAmountPatch
    {
        static void Postfix(ref float __result)
        {
            __result = 1f;
        }
    }

    [HarmonyPatch(typeof(BoostIndicator), nameof(BoostIndicator.ResetIndicator))]
    public static class PreventBoostResetPatch
    {
        static bool Prefix() { return false; }
    }

    [HarmonyPatch(typeof(BoostIndicator), nameof(BoostIndicator.ResetBoostLevels))]
    public static class PreventBoostLevelResetPatch
    {
        static bool Prefix() { return false; }
    }

    [HarmonyPatch(typeof(Cashier), nameof(Cashier.Start))]
    public static class CashierAutoBoostPatch
    {
        static void Postfix(Cashier __instance)
        {
            try
            {
                __instance.SetCashierBoost(3);
                Plugin.Log.LogInfo($"[MaxBoost] Cashier #{__instance.CashierID} -> boost 3");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MaxBoost] Cashier failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(Restocker), nameof(Restocker.Start))]
    public static class RestockerAutoBoostPatch
    {
        static void Postfix(Restocker __instance)
        {
            try
            {
                __instance.SetRestockerBoost(3);
                Plugin.Log.LogInfo($"[MaxBoost] Restocker #{__instance.RestockerID} -> boost 3");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MaxBoost] Restocker failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(CustomerHelper), nameof(CustomerHelper.Start))]
    public static class CustomerHelperAutoBoostPatch
    {
        static void Postfix(CustomerHelper __instance)
        {
            try
            {
                __instance.SetCustomerHelperBoost(3);
                Plugin.Log.LogInfo($"[MaxBoost] CustomerHelper #{__instance.CustomerHelperID} -> boost 3");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MaxBoost] CustomerHelper failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(IceCreamHelper), nameof(IceCreamHelper.Start))]
    public static class IceCreamHelperAutoBoostPatch
    {
        static void Postfix(IceCreamHelper __instance)
        {
            try
            {
                __instance.SetHelperBoost(3);
                Plugin.Log.LogInfo($"[MaxBoost] IceCreamHelper #{__instance.ID} -> boost 3");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MaxBoost] IceCreamHelper failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(Baker), nameof(Baker.Start))]
    public static class BakerAutoBoostPatch
    {
        static void Postfix(Baker __instance)
        {
            try
            {
                __instance.SetBakerBoost(3);
                Plugin.Log.LogInfo($"[MaxBoost] Baker #{__instance.BakerID} -> boost 3");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MaxBoost] Baker failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(__Project__.Scripts.Janitor.Janitor), nameof(__Project__.Scripts.Janitor.Janitor.Start))]
    public static class JanitorAutoBoostPatch
    {
        static void Postfix(__Project__.Scripts.Janitor.Janitor __instance)
        {
            try
            {
                __instance.SetJanitorBoost(3);
                Plugin.Log.LogInfo($"[MaxBoost] Janitor #{__instance.JanitorID} -> boost 3");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MaxBoost] Janitor failed: {e.Message}"); }
        }
    }
}
