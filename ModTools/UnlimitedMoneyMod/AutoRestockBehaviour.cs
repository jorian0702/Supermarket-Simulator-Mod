using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using HarmonyLib;

namespace UnlimitedMoneyMod
{
    public class AutoRestockBehaviour : MonoBehaviour
    {
        public AutoRestockBehaviour(IntPtr ptr) : base(ptr) { }

        public static bool Enabled { get; set; } = false;

        private float _timer = 0f;
        private const float CHECK_INTERVAL = 15f;
        private const int MIN_STOCK = 40;

        void Update()
        {
            if (!Enabled) return;

            _timer += Time.deltaTime;
            if (_timer < CHECK_INTERVAL) return;
            _timer = 0f;

            try { CheckAndRestock(); }
            catch (Exception e) { Plugin.Log.LogError($"[AutoRestock] Error: {e.Message}"); }
        }

        private void CheckAndRestock()
        {
            var inventory = InventoryManager.Instance;
            if (inventory == null) return;

            var licenseManager = ProductLicenseManager.Instance;
            if (licenseManager == null) return;

            var idManager = IDManager.Instance;
            if (idManager == null) return;

            var cartManager = CartManager.Instance;
            if (cartManager == null) return;

            var cart = cartManager.MarketShoppingCart;
            if (cart == null || cart.TooLateToOrderGoods) return;

            var unlockedProductIds = licenseManager.UnlockedProducts;
            if (unlockedProductIds == null || unlockedProductIds.Count == 0) return;

            int freeRackSlots = 0;
            var rm = RackManager.Instance;
            if (rm != null)
            {
                var rackDatas = rm.Data;
                if (rackDatas != null)
                {
                    for (int r = 0; r < rackDatas.Count; r++)
                    {
                        var rd = rackDatas[r];
                        if (rd == null) continue;
                        var rslots = rd.RackSlots;
                        if (rslots == null) continue;
                        for (int j = 0; j < rslots.Count; j++)
                        {
                            var rsd = rslots[j];
                            if (rsd != null && rsd.BoxCount == 0)
                                freeRackSlots++;
                        }
                    }
                }
            }

            const int MAX_ORDER_BOXES = 50;
            int maxBoxesThisOrder = Math.Min(MAX_ORDER_BOXES, Math.Max(freeRackSlots, 10));

            bool needsPurchase = false;
            int totalBoxesAdded = 0;

            for (int i = 0; i < unlockedProductIds.Count; i++)
            {
                if (totalBoxesAdded >= maxBoxesThisOrder) break;

                int productId = unlockedProductIds[i];
                if (!inventory.IsProductDisplayed(productId)) continue;

                int currentStock = inventory.GetInventoryAmount(productId);
                if (currentStock >= MIN_STOCK) continue;

                var productSO = idManager.ProductSO(productId);
                if (productSO == null) continue;

                int needed = MIN_STOCK - currentStock;
                int perBox = productSO.ProductAmountOnPurchase;
                if (perBox <= 0) perBox = 1;

                int boxesNeeded = (int)Math.Ceiling((double)needed / perBox);
                if (boxesNeeded <= 0) boxesNeeded = 1;

                int remaining = maxBoxesThisOrder - totalBoxesAdded;
                if (boxesNeeded > remaining) boxesNeeded = remaining;
                if (boxesNeeded <= 0) break;

                try
                {
                    var item = new ItemQuantity(productId, productSO.BasePrice);
                    item.FirstItemCount = boxesNeeded;

                    bool added = cart.TryAddProduct(item, SalesType.PRODUCT);
                    if (added)
                    {
                        needsPurchase = true;
                        totalBoxesAdded += boxesNeeded;
                        Plugin.Log.LogInfo($"[AutoRestock] Queued {boxesNeeded} boxes of {productSO.ProductName} (stock:{currentStock})");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[AutoRestock] Failed to add {productSO.ProductName}: {e.Message}");
                }
            }

            if (needsPurchase)
            {
                try
                {
                    cart.Purchase(true);
                    Plugin.Log.LogInfo($"[AutoRestock] Auto-purchased {totalBoxesAdded} boxes (free rack slots: {freeRackSlots}, limit: {maxBoxesThisOrder})");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[AutoRestock] Purchase failed: {e.Message}");
                }
            }
        }

        public static void Register()
        {
            ClassInjector.RegisterTypeInIl2Cpp<AutoRestockBehaviour>();

            var go = new GameObject("AutoRestockController");
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<AutoRestockBehaviour>();

            Plugin.Log.LogInfo($"[AutoRestock] Registered (default: OFF, press F1 to toggle)");
        }
    }

    [HarmonyPatch(typeof(MarketShoppingCart), nameof(MarketShoppingCart.CartMaxed))]
    public static class CartMaxedPatch
    {
        static void Postfix(ref bool __result) { __result = false; }
    }

    [HarmonyPatch(typeof(MarketShoppingCart), nameof(MarketShoppingCart.CartMaxedPassive))]
    public static class CartMaxedPassivePatch
    {
        static void Postfix(ref bool __result) { __result = false; }
    }
}
