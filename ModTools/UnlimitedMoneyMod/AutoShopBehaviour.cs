using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using HarmonyLib;

namespace UnlimitedMoneyMod
{
    public class AutoShopBehaviour : MonoBehaviour
    {
        public AutoShopBehaviour(IntPtr ptr) : base(ptr) { }

        public static bool BuyAndPriceEnabled { get; set; } = false;
        public static bool RestockEnabled { get; set; } = false;
        public static bool AutoLabelEnabled { get; set; } = false;

        private float _priceTimer = 5f;
        private float _displayTimer = 8f;
        private float _buyTimer = 15f;
        private float _openBoxTimer = 3f;

        private const float PRICE_INTERVAL = 20f;
        private const float DISPLAY_INTERVAL = 25f;
        private const float BUY_INTERVAL = 60f;
        private const float OPEN_BOX_INTERVAL = 10f;
        private const float PRICE_MARKUP = 0.5f;
        private const int DISPLAY_SLOTS_PER_PRODUCT = 2;

        private Dictionary<DisplayType, FurnitureSO> _furnitureMap;
        private readonly Dictionary<DisplayType, float> _lastBuyTime = new();

        void Update()
        {
            if (_logNeedCooldown > 0f) _logNeedCooldown -= Time.deltaTime;

            if (BuyAndPriceEnabled)
            {
                _priceTimer += Time.deltaTime;
                if (_priceTimer >= PRICE_INTERVAL)
                {
                    _priceTimer = 0f;
                    try { AutoPrice(); }
                    catch (Exception e) { Plugin.Log.LogError($"[AutoPrice] {e.Message}"); }
                }

                _buyTimer += Time.deltaTime;
                if (_buyTimer >= BUY_INTERVAL)
                {
                    _buyTimer = 0f;
                    try { AutoBuyDisplayFurniture(); }
                    catch (Exception e) { Plugin.Log.LogError($"[AutoBuy] {e.Message}"); }
                }

                _openBoxTimer += Time.deltaTime;
                if (_openBoxTimer >= OPEN_BOX_INTERVAL)
                {
                    _openBoxTimer = 0f;
                    try { AutoOpenFurnitureBoxes(); }
                    catch (Exception e) { Plugin.Log.LogError($"[AutoOpenBox] {e.Message}"); }
                }
            }

            if (RestockEnabled || AutoLabelEnabled)
            {
                _displayTimer += Time.deltaTime;
                if (_displayTimer >= DISPLAY_INTERVAL)
                {
                    _displayTimer = 0f;
                    try { AutoLabelAndStock(); }
                    catch (Exception e) { Plugin.Log.LogError($"[AutoDisplay] {e.Message}"); }
                }
            }
        }

        private void BuildFurnitureMap()
        {
            if (_furnitureMap != null) return;

            var idm = IDManager.Instance;
            if (idm == null) return;

            _furnitureMap = new Dictionary<DisplayType, FurnitureSO>();
            var furnitures = idm.Furnitures;

            for (int i = 0; i < furnitures.Count; i++)
            {
                var fso = furnitures[i];
                if (fso == null || fso.FurniturePrefab == null) continue;

                if (fso.DisplaySubType == DisplaySubType.SMALL_RACK ||
                    fso.DisplaySubType == DisplaySubType.TALL_RACK)
                    continue;

                var displayComp = fso.FurniturePrefab.GetComponent<Display>();
                if (displayComp == null) continue;

                var dtype = displayComp.DisplayType;

                if (!_furnitureMap.ContainsKey(dtype) || fso.Cost > _furnitureMap[dtype].Cost)
                {
                    _furnitureMap[dtype] = fso;
                    Plugin.Log.LogInfo($"[AutoShop] Mapped DisplayType.{dtype} -> {fso.FurnitureName} (ID:{fso.ID}, ${fso.Cost:F2})");
                }
            }

            Plugin.Log.LogInfo($"[AutoShop] Furniture map built: {_furnitureMap.Count} display types covered");
        }

        private void AutoOpenFurnitureBoxes()
        {
            var furnitureBoxes = UnityEngine.Object.FindObjectsOfType<FurnitureBox>();
            if (furnitureBoxes == null || furnitureBoxes.Length == 0) return;

            int opened = 0;
            for (int i = 0; i < furnitureBoxes.Length; i++)
            {
                var fbox = furnitureBoxes[i];
                if (fbox == null) continue;

                try
                {
                    string furnitureName = "Unknown";
                    try { furnitureName = fbox.Data?.Furniture?.FurnitureName ?? "Unknown"; } catch { }

                    fbox.OpenBox();
                    opened++;
                    Plugin.Log.LogInfo($"[AutoOpenBox] Opened furniture box: {furnitureName}");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[AutoOpenBox] Failed to open box: {e.Message}");
                }
            }

            if (opened > 0)
                Plugin.Log.LogInfo($"[AutoOpenBox] Auto-opened {opened} furniture boxes");
        }

        private readonly HashSet<int> _autoPricedProducts = new();

        private void EnsurePriceSet(int pid, PriceManager pm)
        {
            try
            {
                if (_autoPricedProducts.Contains(pid)) return;
                if (pm.HasPriceSetByPlayer(pid)) return;
                float cost = pm.CurrentCost(pid);
                if (cost <= 0) return;
                pm.SetPrice_Order(pid, cost + PRICE_MARKUP, 0, -1);
                _autoPricedProducts.Add(pid);
            }
            catch { }
        }

        private void AutoPrice()
        {
            var pm = PriceManager.Instance;
            var lm = ProductLicenseManager.Instance;
            if (pm == null || lm == null) return;

            var ids = lm.UnlockedProducts;
            if (ids == null) return;

            int count = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                int pid = ids[i];
                if (_autoPricedProducts.Contains(pid)) continue;
                if (pm.HasPriceSetByPlayer(pid)) continue;

                float cost = pm.CurrentCost(pid);
                if (cost <= 0) continue;

                pm.SetPrice_Order(pid, cost + PRICE_MARKUP, 0, -1);
                _autoPricedProducts.Add(pid);
                count++;
            }

            if (count > 0)
                Plugin.Log.LogInfo($"[AutoPrice] Set prices for {count} NEW products (market + ${PRICE_MARKUP:F2}), total auto-priced: {_autoPricedProducts.Count}");
        }

        private const int MAX_BOXES_PER_PRODUCT = 2;
        private readonly Dictionary<int, int> _stockedThisCycle = new();
        private bool _ghostCleanupDone = false;
        private float _logNeedCooldown = 0f;

        private void CleanupGhostDisplayData()
        {
            if (_ghostCleanupDone) return;
            _ghostCleanupDone = true;

            var dm = DisplayManager.Instance;
            var im = InventoryManager.Instance;
            var lm = ProductLicenseManager.Instance;
            if (dm == null || im == null || lm == null) return;

            var unlockedIds = lm.UnlockedProducts;
            if (unlockedIds == null) return;

            int cleaned = 0;
            for (int i = 0; i < unlockedIds.Count; i++)
            {
                int pid = unlockedIds[i];
                if (!im.IsProductDisplayed(pid)) continue;

                int actualCount = dm.GetDisplayedProductCount(pid);
                if (actualCount <= 0)
                {
                    try
                    {
                        var iq = new ItemQuantity(pid, 1f);
                        iq.FirstItemCount = 1;
                        im.RemoveProductFromDisplay(iq);
                        cleaned++;
                    }
                    catch { }
                }
            }

            if (cleaned > 0)
                Plugin.Log.LogInfo($"[AutoDisplay] Cleaned {cleaned} ghost displayed-product entries");
        }

        private bool IsActuallyDisplayed(int pid, DisplayManager dm)
        {
            try { return dm.GetDisplayedProductCount(pid) > 0; }
            catch { return false; }
        }

        private void AutoLabelAndStock()
        {
            var dm = DisplayManager.Instance;
            var im = InventoryManager.Instance;
            var idm = IDManager.Instance;
            var lm = ProductLicenseManager.Instance;
            var pm = PriceManager.Instance;
            if (dm == null || im == null || idm == null || lm == null || pm == null) return;

            CleanupGhostDisplayData();

            var displays = UnityEngine.Object.FindObjectsOfType<Display>();
            if (displays == null || displays.Length == 0) return;

            var unlockedIds = lm.UnlockedProducts;
            if (unlockedIds == null) return;

            var productsNeedingDisplay = new List<int>();
            var needByType = new Dictionary<DisplayType, int>();
            for (int i = 0; i < unlockedIds.Count; i++)
            {
                int pid = unlockedIds[i];
                if (IsActuallyDisplayed(pid, dm)) continue;
                productsNeedingDisplay.Add(pid);

                var pso2 = idm.ProductSO(pid);
                if (pso2 != null)
                {
                    var dt = pso2.ProductDisplayType;
                    if (!needByType.ContainsKey(dt)) needByType[dt] = 0;
                    needByType[dt]++;
                }
            }

            if (productsNeedingDisplay.Count > 0 && _logNeedCooldown <= 0f)
            {
                var sb = new System.Text.StringBuilder("[AutoDisplay] Products needing display: ");
                foreach (var kv in needByType)
                    sb.Append($"{kv.Key}={kv.Value} ");
                sb.Append($"(total={productsNeedingDisplay.Count})");
                Plugin.Log.LogInfo(sb.ToString());
                _logNeedCooldown = 120f;
            }

            int stocked = 0;
            int labeled = 0;
            _stockedThisCycle.Clear();

            foreach (var display in displays)
            {
                if (display == null) continue;
                var slots = display.DisplaySlots;
                if (slots == null) continue;

                for (int s = 0; s < slots.Count; s++)
                {
                    var slot = slots[s];
                    if (slot == null) continue;

                    if (RestockEnabled && slot.IsLabelEnabled && !slot.Full && slot.ProductID > 0)
                    {
                        int pid = slot.ProductID;
                        int alreadyStocked = _stockedThisCycle.ContainsKey(pid) ? _stockedThisCycle[pid] : 0;
                        if (alreadyStocked >= MAX_BOXES_PER_PRODUCT) continue;

                        try
                        {
                            var pso = idm.ProductSO(pid);
                            if (pso == null) continue;
                            int fillCount = pso.ProductAmountOnPurchase;
                            if (fillCount <= 0) fillCount = 6;
                            slot.SpawnProduct(pid, fillCount);
                            stocked++;
                            _stockedThisCycle[pid] = alreadyStocked + 1;
                        }
                        catch { }
                    }

                    if (AutoLabelEnabled && slot.IsEmptySlot() && productsNeedingDisplay.Count > 0)
                    {
                        int bestIdx = -1;
                        for (int p = 0; p < productsNeedingDisplay.Count; p++)
                        {
                            var pso = idm.ProductSO(productsNeedingDisplay[p]);
                            if (pso == null) continue;
                            if (pso.ProductDisplayType == display.DisplayType)
                            {
                                bestIdx = p;
                                break;
                            }
                        }

                        if (bestIdx >= 0)
                        {
                            int pid = productsNeedingDisplay[bestIdx];
                            var pso = idm.ProductSO(pid);

                            try
                            {
                                EnsurePriceSet(pid, pm);

                                float price = pm.CurrentCost(pid);
                                if (price <= 0) price = 1f;
                                int fillCount = pso.ProductAmountOnPurchase;
                                if (fillCount <= 0) fillCount = 6;

                                var iq = new ItemQuantity(pid, price + PRICE_MARKUP);
                                iq.FirstItemCount = fillCount;
                                slot.Data = iq;

                                slot.ToggleProductType(pso);
                                slot.SetLabel();

                                dm.AddDisplaySlot(pid, slot);
                                im.AddProductToDisplay(iq);

                                try
                                {
                                    int slotIdx = display.GetIndexOfDisplaySlot(slot);
                                    var displayData = display.Data;
                                    if (displayData != null && displayData.DisplaySlots != null &&
                                        slotIdx >= 0 && slotIdx < displayData.DisplaySlots.Count)
                                    {
                                        displayData.DisplaySlots[slotIdx] = iq;
                                    }
                                }
                                catch { }

                                slot.SpawnProduct(pid, fillCount);

                                try { slot.SetPriceTag(); } catch { }
                                try { slot.PricingChanged(pid); } catch { }
                                try { slot.RequestLabelMaskUpdate(); } catch { }
                                try { dm.OnPriceSet(pid); } catch { }

                                labeled++;
                                _stockedThisCycle[pid] = 1;
                                Plugin.Log.LogInfo($"[AutoDisplay] Labeled+stocked: {pso.ProductName} (ID:{pid}) on {display.DisplayType}");
                            }
                            catch (Exception e)
                            {
                                Plugin.Log.LogWarning($"[AutoDisplay] Failed to label {pso.ProductName}: {e.Message}");
                            }

                            productsNeedingDisplay.RemoveAt(bestIdx);
                        }
                    }
                }
            }

            if (stocked > 0 || labeled > 0)
                Plugin.Log.LogInfo($"[AutoDisplay] Restocked {stocked} slots, labeled {labeled} new slots");
        }

        private void AutoBuyDisplayFurniture()
        {
            BuildFurnitureMap();
            if (_furnitureMap == null) return;

            var cart = CartManager.Instance?.MarketShoppingCart;
            if (cart == null || cart.TooLateToOrderGoods) return;

            var im = InventoryManager.Instance;
            var idm = IDManager.Instance;
            var lm = ProductLicenseManager.Instance;
            if (im == null || idm == null || lm == null) return;

            var unlockedIds = lm.UnlockedProducts;
            if (unlockedIds == null) return;

            var displays = UnityEngine.Object.FindObjectsOfType<Display>();

            var emptySlotsByType = new Dictionary<DisplayType, int>();
            if (displays != null)
            {
                foreach (var display in displays)
                {
                    if (display == null) continue;
                    var slots = display.DisplaySlots;
                    if (slots == null) continue;
                    var dtype = display.DisplayType;

                    if (!emptySlotsByType.ContainsKey(dtype))
                        emptySlotsByType[dtype] = 0;

                    for (int s = 0; s < slots.Count; s++)
                    {
                        var slot = slots[s];
                        if (slot != null && slot.IsEmptySlot())
                            emptySlotsByType[dtype]++;
                    }
                }
            }

            var dm2 = DisplayManager.Instance;
            var neededTypes = new Dictionary<DisplayType, int>();
            for (int i = 0; i < unlockedIds.Count; i++)
            {
                int pid = unlockedIds[i];
                if (dm2 != null && IsActuallyDisplayed(pid, dm2)) continue;

                var pso = idm.ProductSO(pid);
                if (pso == null) continue;

                var dtype = pso.ProductDisplayType;
                if (!neededTypes.ContainsKey(dtype))
                    neededTypes[dtype] = 0;
                neededTypes[dtype]++;
            }

            bool needsPurchase = false;
            float now = Time.time;

            foreach (var kv in neededTypes)
            {
                var dtype = kv.Key;
                int productsNeeding = kv.Value;

                int emptySlots = emptySlotsByType.ContainsKey(dtype) ? emptySlotsByType[dtype] : 0;
                if (emptySlots >= productsNeeding * DISPLAY_SLOTS_PER_PRODUCT) continue;

                if (_lastBuyTime.ContainsKey(dtype) && now - _lastBuyTime[dtype] < 120f) continue;

                if (!_furnitureMap.ContainsKey(dtype))
                {
                    Plugin.Log.LogWarning($"[AutoBuy] No furniture found for DisplayType.{dtype}");
                    continue;
                }

                var fso = _furnitureMap[dtype];
                int furnitureCount = (int)Math.Ceiling((double)(productsNeeding * DISPLAY_SLOTS_PER_PRODUCT - emptySlots) / 8.0);
                if (furnitureCount <= 0) furnitureCount = 1;
                if (furnitureCount > 3) furnitureCount = 3;

                try
                {
                    var item = new ItemQuantity(fso.ID, fso.Cost);
                    item.FirstItemCount = furnitureCount;

                    bool added = cart.TryAddProduct(item, SalesType.SHELF);
                    if (added)
                    {
                        needsPurchase = true;
                        _lastBuyTime[dtype] = now;
                        Plugin.Log.LogInfo($"[AutoBuy] Queued {furnitureCount}x {fso.FurnitureName} for DisplayType.{dtype} ({productsNeeding} products need space)");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[AutoBuy] Failed to add {fso.FurnitureName}: {e.Message}");
                }
            }

            if (needsPurchase)
            {
                try
                {
                    cart.Purchase(true);
                    Plugin.Log.LogInfo("[AutoBuy] Auto-purchased display furniture!");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[AutoBuy] Purchase failed: {e.Message}");
                }
            }
        }

        public static void Register()
        {
            ClassInjector.RegisterTypeInIl2Cpp<AutoShopBehaviour>();

            var go = new GameObject("AutoShopController");
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<AutoShopBehaviour>();

            Plugin.Log.LogInfo($"[AutoShop] Registered -- F2: Buy+Price (OFF), F3: Restock (ON), F4: AutoLabel (OFF)");
        }
    }
}
