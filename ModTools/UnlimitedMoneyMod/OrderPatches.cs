using HarmonyLib;

namespace UnlimitedMoneyMod
{
    // Auto-complete an online order at the moment its customer spawns.
    // DeliverOrder() is the physical handoff step (player puts items in bag, walks to
    // customer, customer takes the bag) — calling it directly with no bag is a no-op.
    // OrderManager.OrderCompleted() is the actual completion path: credits money,
    // removes from active list, despawns the customer.
    static class OrderHelper
    {
        public static bool Complete(OnlineOrderCustomer customer, OrderListData order)
        {
            if (order == null) return false;
            try
            {
                if (customer != null) customer.m_IsOrderDelivered = true;
                var mgr = OrderManager.Instance;
                if (mgr != null) mgr.OrderCompleted(order);
                if (customer != null) { try { customer.DespawnCustomer(); } catch {} }
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[AutoOrder] Complete failed for #{order.ID}: {e.Message}");
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(OrderManager), nameof(OrderManager.CreateCustomer))]
    public static class AutoCompleteOnCustomerCreated
    {
        static void Postfix(OnlineOrderCustomer __result, OrderListData order)
        {
            if (__result == null || order == null) return;
            if (OrderHelper.Complete(__result, order))
                Plugin.Log.LogInfo($"[AutoOrder] Completed #{order.ID} for {order.CustomerName} (${order.TotalPrice:F2} + delivery ${order.DistancePrice:F2})");
        }
    }

    [HarmonyPatch(typeof(OnlineOrderCustomer), nameof(OnlineOrderCustomer.OrderExpired))]
    public static class PreventOrderExpiration
    {
        static bool Prefix(OnlineOrderCustomer __instance)
        {
            OrderHelper.Complete(__instance, __instance?.Order);
            return false;
        }
    }

    [HarmonyPatch(typeof(OnlineOrderCustomer), nameof(OnlineOrderCustomer.OrderCancelled))]
    public static class PreventOrderCancellation
    {
        static bool Prefix(OnlineOrderCustomer __instance)
        {
            OrderHelper.Complete(__instance, __instance?.Order);
            return false;
        }
    }

    // Periodic sweep: catch orders that loaded from save without firing CreateCustomer
    // (LoadFromSave restores OnlineOrderCustomer GameObjects directly), and any orders
    // that slipped through other entry points. Throttled to once every 2 seconds.
    public static class PendingOrderSweeper
    {
        static float _lastSweep = -1f;
        const float Interval = 2f;

        public static void Tick()
        {
            try
            {
                float now = UnityEngine.Time.realtimeSinceStartup;
                if (now - _lastSweep < Interval) return;
                _lastSweep = now;

                var customers = UnityEngine.Object.FindObjectsOfType<OnlineOrderCustomer>(true);
                if (customers == null || customers.Length == 0) return;

                int completed = 0;
                for (int i = 0; i < customers.Length; i++)
                {
                    var c = customers[i];
                    if (c == null || c.gameObject == null) continue;
                    if (c.m_IsOrderDelivered) continue;
                    var order = c.Order;
                    if (order == null) continue;
                    if (OrderHelper.Complete(c, order)) completed++;
                }
                if (completed > 0) Plugin.Log.LogInfo($"[AutoOrder] Sweep completed {completed} pending order(s)");
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[AutoOrder] Sweep err: {e.Message}"); }
        }
    }
}
