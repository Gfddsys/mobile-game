#if UNITY_PURCHASING
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using IapProduct = UnityEngine.Purchasing.Product;
using GameProduct = TurtleBlaster.Product;

namespace TurtleBlaster
{
    /// <summary>
    /// Unity IAP 5.5+ adapter backed by PurchasingController (Fake App Store in Unity Editor,
    /// Google Play / App Store on mobile devices). Uses local code-defined ProductCatalog.
    /// </summary>
    public sealed class UnityIapPurchaseService : IPurchaseService, IDisposable
    {
        const string TransactionKeyPrefix = "turtleblaster.iap.tx.";

        PurchasingController controller;
        string storeName;
        bool initializing;
        bool readyToBuy;
        bool disposed;
        string lastError;

        readonly Dictionary<string, IapProduct> fetchedProducts = new Dictionary<string, IapProduct>();
        string activePurchaseProductId;
        Action<bool> pendingPurchaseCallback;
        Action<IList<string>> pendingRestoreCallback;
        LifecycleHook lifecycleHook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Services.Purchases = new UnityIapPurchaseService();
        }

        public bool IsAvailable => readyToBuy && controller != null;

        public string StoreStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(lastError)) return "IAP Error: " + lastError;
                if (!readyToBuy) return "Connecting to Unity IAP Store...";
                return Application.isEditor
                    ? $"UNITY IAP 5.5 ({storeName} / Fake Store) - Ready to purchase."
                    : string.Empty;
            }
        }

        public void Initialize()
        {
            if (disposed || initializing || readyToBuy) return;
            EnsureLifecycleHook();
            initializing = true;
            lastError = null;
            InitializeAsync();
        }

        void EnsureLifecycleHook()
        {
            if (lifecycleHook != null || !Application.isPlaying) return;
            var go = new GameObject("[UnityIapLifecycle]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            lifecycleHook = go.AddComponent<LifecycleHook>();
            lifecycleHook.Owner = this;
        }

        async void InitializeAsync()
        {
            try
            {
                storeName = UnityIAPServices.GetDefaultStore();
                controller = new PurchasingController(storeName);

                controller.OnStoreConnected += OnStoreConnected;
                controller.OnStoreDisconnected += OnStoreDisconnected;
                controller.OnProductsFetched += OnProductsFetched;
                controller.OnProductsFetchFailed += OnProductsFetchFailed;
                controller.OnPurchasesFetched += OnPurchasesFetched;
                controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                controller.OnPurchasePending += OnPurchasePending;
                controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                controller.OnPurchaseFailed += OnPurchaseFailed;

                await controller.Connect();
                if (disposed) return;

                var catalog = new CatalogProvider();
                foreach (var p in ProductCatalog.All)
                {
                    ProductType pt = p.kind == ProductKind.Coins ? ProductType.Consumable
                        : p.kind == ProductKind.Subscription ? ProductType.Subscription
                        : ProductType.NonConsumable;
                    catalog.AddProduct(p.id, pt);
                }

                controller.FetchProductsWithNoRetries(catalog.GetProducts(storeName), storeName);
            }
            catch (Exception ex)
            {
                initializing = false;
                readyToBuy = false;
                lastError = ex.Message;
                Debug.LogException(ex);
                Services.NotifyPurchasesChanged();
            }
        }

        public string GetPriceString(string productId)
        {
            if (!string.IsNullOrEmpty(productId) &&
                fetchedProducts.TryGetValue(productId, out var ip) &&
                ip != null && ip.metadata != null &&
                !string.IsNullOrEmpty(ip.metadata.localizedPriceString))
            {
                return ip.metadata.localizedPriceString;
            }
            return ProductCatalog.Get(productId)?.mockPrice ?? string.Empty;
        }

        public void Purchase(string productId, Action<bool> onResult)
        {
            if (!readyToBuy || controller == null || pendingPurchaseCallback != null)
            {
                Debug.LogWarning("[IAP] Store not ready or another purchase is in progress.");
                onResult?.Invoke(false);
                return;
            }

            activePurchaseProductId = productId;
            pendingPurchaseCallback = onResult;
            controller.PurchaseProduct(productId, storeName);
        }

        public void RestorePurchases(Action<IList<string>> onRestored)
        {
            if (!readyToBuy || controller == null)
            {
                onRestored?.Invoke(new List<string>());
                return;
            }

            pendingRestoreCallback = onRestored;
            controller.FetchPurchases(storeName);
        }

        void OnStoreConnected(string source)
        {
            Debug.Log($"[IAP] Connected to {source}.");
        }

        void OnStoreDisconnected(string source, StoreConnectionFailureDescription failure)
        {
            initializing = false;
            readyToBuy = false;
            lastError = failure != null ? failure.message : "Store disconnected";
            Debug.LogError($"[IAP] Store disconnected ({source}): {lastError}");
            FlushPendingPurchase(false);
            FlushPendingRestore(new List<string>());
            Services.NotifyPurchasesChanged();
        }

        void OnProductsFetched(string source, List<IapProduct> products)
        {
            fetchedProducts.Clear();
            if (products != null)
            {
                foreach (var product in products)
                {
                    if (product?.definition == null) continue;
                    fetchedProducts[product.definition.id] = product;
                    Debug.Log($"[IAP] Product fetched: {product.definition.id} ({product.definition.type}) - {product.metadata?.localizedPriceString}");
                }
            }
            Debug.Log($"[IAP] Fetched {fetchedProducts.Count} products from {source}.");
            controller?.FetchPurchases(source);
            Services.NotifyPurchasesChanged();
        }

        void OnProductsFetchFailed(string source, ProductFetchFailed failure)
        {
            initializing = false;
            readyToBuy = false;
            lastError = failure != null ? failure.FailureReason.ToString() : "Products fetch failed";
            Debug.LogError($"[IAP] Products fetch failed ({source}): {lastError}");
            Services.NotifyPurchasesChanged();
        }

        void OnPurchasesFetched(string source, Orders orders)
        {
            var restoredIds = new List<string>();
            if (orders != null && orders.ConfirmedOrders != null)
            {
                bool anySkinRestored = false;
                foreach (var order in orders.ConfirmedOrders)
                {
                    if (order?.CartOrdered == null) continue;
                    foreach (var item in order.CartOrdered.Items())
                    {
                        string id = item?.Product?.definition?.id;
                        GameProduct gp = ProductCatalog.Get(id);
                        if (gp != null && gp.kind == ProductKind.Skin)
                        {
                            restoredIds.Add(id);
                            if (!SaveData.Current.OwnsSkin(gp.skinId))
                            {
                                SaveData.Current.GrantSkin(gp.skinId);
                                anySkinRestored = true;
                            }
                        }
                    }
                }
                if (anySkinRestored) SaveData.Current.Save();
            }

            initializing = false;
            readyToBuy = true;
            lastError = null;
            Debug.Log($"[IAP] Purchase history loaded ({source}). Ready to buy.");
            FlushPendingRestore(restoredIds);
            Services.NotifyPurchasesChanged();
        }

        void OnPurchasesFetchFailed(string source, PurchasesFetchFailureDescription failure)
        {
            initializing = false;
            readyToBuy = false;
            lastError = failure != null ? failure.Message : "Purchases fetch failed";
            Debug.LogWarning($"[IAP] Purchase-history fetch failed ({source}): {failure?.FailureReason} - {lastError}");
            FlushPendingRestore(new List<string>());
            Services.NotifyPurchasesChanged();
        }

        void OnPurchasePending(string source, PendingOrder order)
        {
            string transactionId = order?.Info?.TransactionID;
            if (string.IsNullOrEmpty(transactionId))
            {
                Debug.LogError("[IAP] Missing transaction ID. Order remains pending.");
                return;
            }

            var validItems = new List<(GameProduct prod, int qty)>();
            if (order.CartOrdered != null)
            {
                foreach (var item in order.CartOrdered.Items())
                {
                    string id = item?.Product?.definition?.id;
                    GameProduct gp = ProductCatalog.Get(id);
                    if (gp == null || item.Quantity <= 0)
                    {
                        Debug.LogError($"[IAP] Unknown product or invalid quantity in pending order: '{id}'.");
                        return;
                    }
                    validItems.Add((gp, item.Quantity));
                }
            }

            if (validItems.Count == 0)
            {
                Debug.LogError("[IAP] Pending order contains no items.");
                return;
            }

            string txKey = TransactionKeyPrefix + source + ":" + transactionId;
            bool matchesActive = pendingPurchaseCallback != null && OrderContainsProduct(validItems, activePurchaseProductId);

            if (PlayerPrefs.GetInt(txKey, 0) == 1)
            {
                Debug.Log($"[IAP] Already handled transaction {transactionId}; confirming without duplicate grant.");
                controller?.ConfirmPurchase(order);
                if (matchesActive)
                {
                    Services.LastPurchaseAlreadyGranted = true;
                    FlushPendingPurchase(true);
                }
                Services.NotifyPurchasesChanged();
                return;
            }

            if (matchesActive)
            {
                Services.LastPurchaseAlreadyGranted = false;
                FlushPendingPurchase(true);
                // Grant any additional items if a bundle contained extra lines
                foreach (var entry in validItems)
                {
                    if (entry.prod.id == activePurchaseProductId)
                    {
                        for (int q = 1; q < entry.qty; q++) Store.Grant(entry.prod);
                    }
                    else
                    {
                        for (int q = 0; q < entry.qty; q++) Store.Grant(entry.prod);
                    }
                }
            }
            else
            {
                foreach (var entry in validItems)
                {
                    for (int q = 0; q < entry.qty; q++) Store.Grant(entry.prod);
                }
            }

            PlayerPrefs.SetInt(txKey, 1);
            PlayerPrefs.Save();
            controller?.ConfirmPurchase(order);
            Services.NotifyPurchasesChanged();
        }

        void OnPurchaseConfirmed(string source, Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogError($"[IAP] Confirmation failed ({source}): {failed.FailureReason} - {failed.Details}");
            else
                Debug.Log($"[IAP] Purchase confirmed ({source}).");
            Services.NotifyPurchasesChanged();
        }

        void OnPurchaseFailed(string source, FailedOrder failed)
        {
            Debug.LogWarning($"[IAP] Purchase failed ({source}): {failed?.FailureReason} - {failed?.Details}");
            FlushPendingPurchase(false);
        }

        static bool OrderContainsProduct(List<(GameProduct prod, int qty)> items, string productId)
        {
            if (string.IsNullOrEmpty(productId)) return false;
            for (int i = 0; i < items.Count; i++)
                if (items[i].prod.id == productId) return true;
            return false;
        }

        void FlushPendingPurchase(bool success)
        {
            var cb = pendingPurchaseCallback;
            pendingPurchaseCallback = null;
            activePurchaseProductId = null;
            cb?.Invoke(success);
        }

        void FlushPendingRestore(IList<string> restoredIds)
        {
            var cb = pendingRestoreCallback;
            pendingRestoreCallback = null;
            cb?.Invoke(restoredIds);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            initializing = false;
            readyToBuy = false;
            if (controller != null)
            {
                controller.OnStoreConnected -= OnStoreConnected;
                controller.OnStoreDisconnected -= OnStoreDisconnected;
                controller.OnProductsFetched -= OnProductsFetched;
                controller.OnProductsFetchFailed -= OnProductsFetchFailed;
                controller.OnPurchasesFetched -= OnPurchasesFetched;
                controller.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
                controller.OnPurchasePending -= OnPurchasePending;
                controller.OnPurchaseConfirmed -= OnPurchaseConfirmed;
                controller.OnPurchaseFailed -= OnPurchaseFailed;
                controller = null;
            }
        }

        sealed class LifecycleHook : MonoBehaviour
        {
            public UnityIapPurchaseService Owner;
            void OnDestroy() => Owner?.Dispose();
            void OnApplicationQuit() => Owner?.Dispose();
        }
    }
}
#endif
