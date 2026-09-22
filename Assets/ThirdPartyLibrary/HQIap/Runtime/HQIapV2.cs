using System;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
// using UnityEngine.Purchasing.Security;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using System.Collections;
public enum HQIapPurchaseResult
{
    None,
    Failed,// 失败
    Succeeded,// 成功
    NotInitialized,// 初始化未完成
}

public enum HQIapStatus
{
    None,// 无状态
    Failed,// 失败
    Succeeded,// 成功
    NotInitialized,// 初始化未完成
    InProgress,// 购买正在处理中 - 初始未完成时，会等待n秒后再发起购买，此时重复发起购买会触发此状态
    ClientDataError, //客户端数据错误
    ClientCodeError,// 客户端代码错误
    StoreError,// 商店错误
    UnknownError// 未知错误
}

public class HQIap : MonoBehaviour, IDetailedStoreListener
{
    /// <summary>
    /// 模拟支付:开启后真实支付不会调用
    /// 即使内购初始化未完成也可以发起购买逻辑
    /// </summary>
    public static bool EnableSimulatedPurchase = false;


    /// <summary>
    /// 支付购买的结果
    /// 1、!!!内购成功后"只能"在此处操作用户数据，金币，免广告等
    /// 2、用户数据处理完毕后请返回true，告诉系统处理完毕了否则启动时或恢复购买时，会持续收到系统回调
    /// 3、发起购买后退出应用，在应用外输入密码购买完成，在下一次启动时，也会在此处收到购买结果
    /// </summary>
    public delegate bool PayoutProductEvent(HQIapProduct product);

    /// <summary>
    /// 内购初始化结果
    /// </summary>
    public delegate void InitializedEvent(HQIapStatus status);

    const string k_ResEnvironment = "production";
    const string k_DevEnvironment = "development";
    const string k_IapDisableSimulatedPurchase = "__IapDisableSimulatedPurchase__";
    private Dictionary<string, HQIapProduct> _products;
    private IStoreController _storeController;
    private IExtensionProvider _extensionProvider;
    private PayoutProductEvent _payoutProductEvent;
    private InitializedEvent _initializedEvent;
    // private CrossPlatformValidator _validator = null;
    private bool _initialized { get { return _storeController != null; } }
    private int _InitUnityServicesRetries = 0;
    private Action<string, HQIapPurchaseResult> _purchaseResultHandler;
    private Action<HQIapPurchaseResult> _restoreResultHandler;
    private Dictionary<string, string> _prodictIdScene = new Dictionary<string, string>();
    private int _tryInitUnityPurchaseCount = 0;//尝试初始化次数
    #region 对外接口
    /// <summary>
    /// 初始化iap
    /// 可以指定是否自动初始化UnityServices
    /// /// </summary>
    public static void Init(InitializedEvent initializedEvent, PayoutProductEvent payoutProductEvent, bool autoInitializeUnityServices = true)
    {
        Instance._Init(initializedEvent, payoutProductEvent, autoInitializeUnityServices);
    }

    /// <summary>
    /// 发起购买
    /// !!!此处购买完成的回调只能操作UI相关，数字增加，或者飞金币等动效
    /// </summary>
    public static void Purchase(string id, Action<string, HQIapPurchaseResult> resultHandler, string scene = null)
    {
        Instance._Purchase(id, resultHandler, scene);
    }

    public static void Restore(Action<HQIapPurchaseResult> resultHandler = null)
    {
        Instance._Restore(resultHandler);
    }

    public static bool IsSubscriptionActive(string id)
    {
        return Instance._IsSubscriptionActive(id);
    }

    public static HQIapProduct GetProduct(string id)
    {
        return Instance._GetProduct(id);
    }

    public static HQIapProduct[] GetProducts()
    {
        return Instance._GetProducts();
    }

    public static void DisableSimulatedPurchase()
    {
        PlayerPrefs.SetInt(k_IapDisableSimulatedPurchase, 1);
        PlayerPrefs.Save();
        EnableSimulatedPurchase = false;
    }
    #endregion



    #region 业务代码
    private void _Init(InitializedEvent initializedEvent, PayoutProductEvent payoutProductEvent, bool autoInitializeUnityServices = true)
    {
        DebugLog("Init");
        HQIapEvent.Init();
        if (_initialized)
        {
            HQIapEvent.IapInitFail(HQIapStatus.ClientCodeError, "Repeat Init", 0);
            initializedEvent(HQIapStatus.ClientCodeError);
            return;
        }

        if (payoutProductEvent == null)
        {
            DebugLogError($"Init Error:无法完成初始化,必须设置PayoutProductEvent");
            return;
        }

        _payoutProductEvent = payoutProductEvent;
        _initializedEvent = initializedEvent;

        if (autoInitializeUnityServices && UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            InitUnityServices(() => InitProducts());
        }
        else if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            InitProducts();
        }
        else
        {
            InitProducts();
        }

#if hq_debug
        if (HQDebugging.IsDebug && !PlayerPrefs.HasKey(k_IapDisableSimulatedPurchase))
#else
        if (Debug.isDebugBuild && !PlayerPrefs.HasKey(k_IapDisableSimulatedPurchase))
#endif


        {
            EnableSimulatedPurchase = true;
        }
    }

    /// <summary>
    /// 发起购买
    /// !!!此处购买完成的回调只能操作UI相关，数字增加，或者飞金币等动效
    /// </summary>
    private void _Purchase(string id, Action<string, HQIapPurchaseResult> resultHandler, string scene = null)
    {
        DebugLog($"Purchase 发起购买:{id}");
        _purchaseResultHandler = resultHandler;

        //防止意外,增加一层非isDebugBuild判断

#if UNITY_EDITOR
        if (EnableSimulatedPurchase)
#elif hq_debug
        if (HQDebugging.IsDebug && EnableSimulatedPurchase)
#else
        if (Debug.isDebugBuild && EnableSimulatedPurchase)
#endif
        {
            _SimulatedPurchase(id, resultHandler, scene);
            return;
        }

        if (!_initialized)
        {
            DebugLog("Purchase " + " Iap初始化未完成");
            PurchaseResultHandler(id, HQIapPurchaseResult.NotInitialized);
            HQIapEvent.IapBuyFail(id, null, scene, HQIapStatus.NotInitialized, "Not Initialized");
            return;
        }

        if (string.IsNullOrEmpty(id))
        {
            DebugLog("Purchase " + " 商品id为空");
            PurchaseResultHandler(id, HQIapPurchaseResult.Failed);
            HQIapEvent.IapBuyFail(id, null, scene, HQIapStatus.ClientDataError, "Purchase ProductId == null");
            return;
        }


        if (_products == null || !_products.ContainsKey(id))//商品不存在
        {
            DebugLog($"Purchase " + " 商品表为空或者'{id}'不存在");
            PurchaseResultHandler(id, HQIapPurchaseResult.Failed);
            HQIapEvent.IapBuyFail(id, null, scene, HQIapStatus.ClientDataError, "Purchase ProductId not found in 'iap_data.json'");
            return;
        }

        Product unityProduct = _storeController.products.WithID(id);
        if (unityProduct == null || unityProduct.availableToPurchase == false)//商品不可用
        {
            DebugLog("Purchase " + "商品不存在 or availableToPurchase == false");
            PurchaseResultHandler(id, HQIapPurchaseResult.Failed);
            HQIapEvent.IapBuyFail(id, null, scene, HQIapStatus.ClientDataError, "Purchase Product == null");
            return;
        }

        if (scene == null)
        {
            _prodictIdScene.Remove(id);
        }
        else
        {
            _prodictIdScene[id] = scene;
        }

        _storeController.InitiatePurchase(id);
    }


    private void _SimulatedPurchase(string id, Action<string, HQIapPurchaseResult> resultHandler, string scene = null)
    {
        HQIapSimulatedView simulatedView = HQIapSimulatedView.ShowView();

        simulatedView.LoadData(id, scene, _initialized, _products);

        simulatedView.OnSimulatedPayment = (HQIapProduct iapProduct) =>
        {
            iapProduct.PurchaseScene = scene;
            iapProduct.TransactionID = $"simulation:{id}:{Guid.NewGuid():N}";
            iapProduct.Receipt = null;
            iapProduct.IsRestored = false;
            bool result = _payoutProductEvent(iapProduct);
            DebugLog($"模拟购买处理结果:{result}");
            PurchaseResultHandler(id, result ? HQIapPurchaseResult.Succeeded : HQIapPurchaseResult.Failed);
        };

        simulatedView.OnSimulatedCancel = () =>
        {
            PurchaseResultHandler(id, HQIapPurchaseResult.Failed);
        };
    }

    private void _Restore(Action<HQIapPurchaseResult> resultHandler = null)
    {
        DebugLog("Restore");
        if (!_initialized)
        {
            HQIapEvent.IapRestoreFail(HQIapStatus.NotInitialized, "Not Initialized");
            resultHandler?.Invoke(HQIapPurchaseResult.NotInitialized);
            return;
        }

        _restoreResultHandler = resultHandler;

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
                         Application.platform == RuntimePlatform.OSXPlayer ||
                         Application.platform == RuntimePlatform.tvOS)
        {
            _extensionProvider.GetExtension<IAppleExtensions>().RestoreTransactions(OnRestored);
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            _extensionProvider.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions(OnRestored);
        }
    }

    public bool _IsSubscriptionActive(string id)
    {
        DebugLog($"IsSubscriptionActive:{id}");
        if (!_initialized)
        {
            HQIapEvent.IapSubscriptionError(HQIapStatus.NotInitialized, "IsSubscriptionActive");
            return false;
        }

        if (string.IsNullOrEmpty(id))
        {
            DebugLog("IsSubscriptionActive " + " ProductId is empty");
            HQIapEvent.IapSubscriptionError(HQIapStatus.ClientDataError, "IsSubscriptionActive ProductId == null");
            return false;
        }
        var product = _storeController.products.WithID(id);
        if (product == null)
        {
            DebugLog("IsSubscriptionActive " + " ProductId is not exist");
            HQIapEvent.IapSubscriptionError(HQIapStatus.ClientDataError, "IsSubscriptionActive Product not exist");
            return false;
        }

        try
        {
            var isSubscribed = IsSubscribedTo(product);
            return isSubscribed;
        }
        catch (StoreSubscriptionInfoNotSupportedException)
        {
            var receipt = (Dictionary<string, object>)MiniJson.JsonDecode(product.receipt);
            var store = receipt["Store"];
            string text =
                "Couldn't retrieve subscription information because your current store is not supported.\n" +
                $"Your store: \"{store}\"\n\n" +
                "You must use the App Store, Google Play Store or Amazon Store to be able to retrieve subscription information.\n\n" +
                "For more information, see README.md";
            DebugLog("IsSubscriptionActive " + text);

            HQIapEvent.IapSubscriptionError(HQIapStatus.StoreError, "IsSubscriptionActive Exception");

            return false;

        }
    }

    private HQIapProduct _GetProduct(string id)
    {
        // if (!_initialized)
        // {
        //     LogError("GetProduct Not Initialized");
        //     return null;
        // }
        if (_products == null)
        {
            DebugLogError("_products == null");
            return null;
        }

        if (_products.TryGetValue(id, out HQIapProduct product))
        {
            return product;
        }
        else
        {
            return null;
        }
    }

    private HQIapProduct[] _GetProducts()
    {
        // if (!_initialized)
        // {
        //     LogError("GetProduct Not Initialized");
        //     return null;
        // }
        if (_products == null)
        {
            DebugLogError("_products == null");
            return null;
        }

        return _products.Values.ToArray();
    }

    void InitializedHandler(HQIapStatus status)
    {
        _initializedEvent?.Invoke(status);
    }
    void Update()
    {
        if (HQIapEvent.StartUnityTimer)
        {
            HQIapEvent.UnityDuration += Time.deltaTime;
        }

        if (HQIapEvent.StartStoreTimer)
        {
            HQIapEvent.StoreDuration += Time.deltaTime;
        }
    }

    void InitProducts()
    {
        DebugLog("InitProducts 进入");
        HQIapEvent.StartUnityTimer = false;
        HQIapEvent.StartStoreTimer = true;
        try
        {
            TextAsset textAsset = Resources.Load<TextAsset>("hq_iap_data");
            HQIapProduct[] products = JsonConvert.DeserializeObject<HQIapProduct[]>(textAsset.text);
            _products = products.ToDictionary(p => p.ID);
        }
        catch (Exception e)
        {
            DebugLog($"InitProducts Error:{e.Message}");
            HQIapEvent.IapInitFail(HQIapStatus.ClientDataError, "hq_iap_data.json Decode Error", 0);
            InitializedHandler(HQIapStatus.ClientDataError);
            return;
        }

        List<string> Ids = new List<string>();
        foreach (var item in _products)
        {
            Ids.Add(item.Value.ID);
        }

        DebugLog($"InitProducts 准备本地商品 数量:{Ids.Count} IDs:{JsonConvert.SerializeObject(Ids)}");

        InitUnityIap();
    }

    void InitUnityIap()
    {
        DebugLog("InitUnityIap 进入");
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        List<string> storeIds = new List<string>();
        foreach (var product in _products.Values)
        {
            // if (_tryInitUnityPurchaseCount < 2)//模拟前两次拉不到商品
            // {
            //     continue;
            // }
            IDs storeId = null;
#if UNITY_EDITOR
            storeIds.Add(product.AppStoreID);
            storeId = new IDs { { product.AppStoreID, AppleAppStore.Name }, { product.GooglePlayID, GooglePlay.Name } };
#else

            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                         Application.platform == RuntimePlatform.OSXPlayer ||
                         Application.platform == RuntimePlatform.tvOS)
            {
                storeIds.Add(product.AppStoreID);
                storeId = new IDs { { product.AppStoreID, AppleAppStore.Name }, };
                if (string.IsNullOrEmpty(product.AppStoreID))
                {
                    DebugLog($"ID:{product.ID} AppStoreID 为空");
                }
            }
            else if (Application.platform == RuntimePlatform.Android)
            {
                storeIds.Add(product.GooglePlayID);
                storeId = new IDs { { product.GooglePlayID, GooglePlay.Name } };
                if (string.IsNullOrEmpty(product.GooglePlayID))
                {
                    DebugLog($"ID:{product.ID} GooglePlayID 为空");
                }
            }
#endif
            builder.AddProduct(product.ID, product.ProductType, storeId);
        }

        DebugLog($"InitUnityIap 初始化商店商品 数量:{storeIds.Count} StoreIds:{JsonConvert.SerializeObject(storeIds)}");
        DebugLog("InitUnityIap 发起UnityIap初始化");
        UnityPurchasing.Initialize(this, builder);
    }

    void PurchaseResultHandler(string id, HQIapPurchaseResult result)
    {
        try
        {
            _purchaseResultHandler?.Invoke(id, result);
            _purchaseResultHandler = null;
        }
        catch (Exception e)
        {
            DebugLogError($"PurchaseResultHandler Error:{e.Message}");
        }

    }

    bool IsSubscribedTo(Product subscription)
    {
        // If the product doesn't have a receipt, then it wasn't purchased and the user is therefore not subscribed.
        if (subscription.receipt == null)
        {
            return false;
        }

        //The intro_json parameter is optional and is only used for the App Store to get introductory information.
        var subscriptionManager = new SubscriptionManager(subscription, null);

        // The SubscriptionInfo contains all of the information about the subscription.
        // Find out more: https://docs.unity3d.com/Packages/com.unity.purchasing@3.1/manual/UnityIAPSubscriptionProducts.html
        var info = subscriptionManager.getSubscriptionInfo();

        return info.isSubscribed() == Result.True;
    }

    IEnumerator TryInitUnityIap()
    {
        yield return new WaitForSeconds(5 * (_tryInitUnityPurchaseCount + 1));
        InitUnityIap();
    }
    #endregion


    #region 内购验证
    //     private bool IsPurchaseValid(Product product)
    //     {
    //         //If we the validator doesn't support the current store, we assume the purchase is valid
    //         if (IsCurrentStoreSupportedByValidator())
    //         {
    //             try
    //             {
    //                 var result = _validator.Validate(product.receipt);

    //                 //The validator returns parsed receipts.
    //                 // LogReceipts(result);
    //             }

    //             //If the purchase is deemed invalid, the validator throws an IAPSecurityException.
    //             catch (IAPSecurityException reason)
    //             {
    //                 Debug.Log($"Invalid receipt: {reason}");
    //                 return false;
    //             }
    //         }

    //         return true;
    //     }
    //     private bool IsCurrentStoreSupportedByValidator()
    //     {
    //         //The CrossPlatform validator only supports the GooglePlayStore and Apple's App Stores.
    //         return IsGooglePlayStoreSelected() || IsAppleAppStoreSelected();
    //     }

    //     private bool IsGooglePlayStoreSelected()
    //     {
    //         var currentAppStore = StandardPurchasingModule.Instance().appStore;
    //         return currentAppStore == AppStore.GooglePlay;
    //     }

    //     private bool IsAppleAppStoreSelected()
    //     {
    //         var currentAppStore = StandardPurchasingModule.Instance().appStore;
    //         return currentAppStore == AppStore.AppleAppStore ||
    //             currentAppStore == AppStore.MacAppStore;
    //     }
    //     void InitValidator()
    //     {
    //         if (IsCurrentStoreSupportedByValidator())
    //         {
    // #if !UNITY_EDITOR
    //                 // var appleTangleData = m_UseAppleStoreKitTestCertificate ? AppleStoreKitTestTangle.Data() : AppleTangle.Data();
    //                 var appleTangleData = AppleTangle.Data();
    //                 _validator = new CrossPlatformValidator(GooglePlayTangle.Data(), appleTangleData, Application.identifier);
    // #endif
    //         }
    //         // else
    //         // {
    //         //     userWarning.WarnInvalidStore(StandardPurchasingModule.Instance().appStore);
    //         // }
    //     }

    #endregion

    #region Unity Services
    void InitUnityServices(Action onSuccess)
    {
        DebugLog("InitUnityServices 进入");
        HQIapEvent.StartUnityTimer = true;
        TryInitUnityServices(onSuccess);
    }

    void TryInitUnityServices(Action onSuccess)
    {
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            onSuccess();
            return;
        }

        try
        {
#if hq_debug
            string environmentName = HQDebugging.IsDebug ? k_DevEnvironment : k_ResEnvironment;
#else
            string environmentName = Debug.isDebugBuild ? k_DevEnvironment : k_ResEnvironment;
#endif


            var options = new InitializationOptions().SetEnvironmentName(environmentName);

            UnityServices.InitializeAsync(options).ContinueWith((Task task) =>
            {
                DebugLog("InitUnityServices 成功");
                onSuccess();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception exception)
        {
            DebugLog($"InitUnityServices 失败 第{_InitUnityServicesRetries + 1}次尝试 Message:{exception.Message}");
            if (_InitUnityServicesRetries < 3)
            {
                _InitUnityServicesRetries++;
                int delayInSeconds = 2; // 设置等待时间，单位秒
                Task.Delay(TimeSpan.FromSeconds(delayInSeconds))
                    .ContinueWith(_ => TryInitUnityServices(onSuccess));
            }
            else
            {
                DebugLog($"InitUnityServices 初始化没有成功,进行下一个流程!!!");

                //强行初始化
                onSuccess();
            }

        }
    }
    #endregion


    #region Unity Iap 回调

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        DebugLog("OnInitialized 初始化完成");
        HQIapEvent.StartStoreTimer = false;

        _storeController = controller;
        _extensionProvider = extensions;

        //扩充商品信息
        List<string> productIds = new List<string>();
        foreach (var item in _storeController.products.all)
        {
            if (_products.TryGetValue(item.definition.id, out HQIapProduct product))
            {
                if (item.metadata != null)
                {
                    productIds.Add(item.definition.storeSpecificId);
                    product.localizedTitle = item.metadata.localizedTitle;
                    product.localizedPriceString = item.metadata.localizedPriceString;
                    product.localizedDescription = item.metadata.localizedDescription;
                    product.isoCurrencyCode = item.metadata.isoCurrencyCode;
                    product.localizedPrice = item.metadata.localizedPrice;
                }
            }
        }


        DebugLog($"OnInitialized 初始化完成 数量:{productIds.Count} StoreIds:{JsonConvert.SerializeObject(productIds)}");

        try
        {
            HQIapEvent.IapInitSucc(controller.products.all.Length, _tryInitUnityPurchaseCount);
            InitializedHandler(HQIapStatus.Succeeded);
        }
        catch (System.Exception e)
        {
            DebugLogError($"OnInitialized Error:{e.Message}");
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        OnInitializeFailed(error, null);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        HQIapEvent.StartStoreTimer = false;
        var errorMessage = $"Purchasing failed to initialize. Reason: {error}.";

        if (message != null)
        {
            errorMessage += $" More details: {message}";
        }

        Debug.Log(errorMessage);


        if (_tryInitUnityPurchaseCount >= 3)
        {
            DebugLog($"OnInitializeFailed 初始化失败: {errorMessage}");
            HQIapEvent.IapInitFail(HQIapStatus.StoreError, error.ToString(), _tryInitUnityPurchaseCount);
            InitializedHandler(HQIapStatus.StoreError);
        }
        else
        {
            DebugLog($"OnInitializeFailed 初始化失败,5秒后准备重试: {errorMessage}");
            StartCoroutine(TryInitUnityIap());
        }

        _tryInitUnityPurchaseCount++;
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        DebugLog("ProcessPurchase 进入");

        var product = args.purchasedProduct;
        if (product == null)
        {
            DebugLog("Product Info: 数据为空！！！");
            HQIapEvent.IapBuyFail(null, null, null, HQIapStatus.ClientDataError, "PurchaseEventArgs.purchasedProduct == null");
            return PurchaseProcessingResult.Complete;
        }
        var productId = product.definition?.id;
        var storeId = product.definition?.storeSpecificId;
        string infoMsg = "Unity Product Info: \n";
        infoMsg += $"ID:{productId}";
        infoMsg += $"StoreId:{product.definition.storeSpecificId}";
        infoMsg += $"Metadata:{JsonConvert.SerializeObject(product.metadata)}";
        DebugLog(infoMsg);

        string scene = null;
        if (productId != null)
        {
            _prodictIdScene.TryGetValue(productId, out scene);
        }



        HQIapProduct iapProduct = null;
        _products.TryGetValue(productId, out iapProduct);

        if (iapProduct == null)
        {
            DebugLog("ProcessPurchase HQIapProduct No exists!!!");
            HQIapEvent.IapBuyFail(productId, null, scene, HQIapStatus.ClientDataError, "ProcessPurchase HQIapProduct == null");
            return PurchaseProcessingResult.Pending;
        }

        bool result = false;
        try
        {
            iapProduct.PurchaseScene = scene;
            iapProduct.TransactionID = product.transactionID;
            iapProduct.Receipt = product.receipt;
            iapProduct.IsRestored = product.appleProductIsRestored;
            result = _payoutProductEvent(iapProduct);
        }
        catch (Exception e)
        {
            PurchaseResultHandler(productId, HQIapPurchaseResult.Failed);
            HQIapEvent.IapBuyFail(productId, storeId, scene, HQIapStatus.ClientCodeError, $"ProcessPurchase PayoutProductEvent Error:{e.Message}");
            return PurchaseProcessingResult.Pending;
        }

        if (result)
        {
            PurchaseResultHandler(productId, HQIapPurchaseResult.Succeeded);

            if (product.appleProductIsRestored == false)
            {
                HQIapEvent.IapBuySucc(iapProduct, product.appleProductIsRestored, product.transactionID, scene);
            }

            DebugLog("ProcessPurchase Complete");
            return PurchaseProcessingResult.Complete;
        }
        else
        {
            DebugLog("ProcessPurchase Pending End");
            return PurchaseProcessingResult.Pending;
        }
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        DebugLog("OnPurchaseFailed 购买失败");

        var productId = product?.definition?.id;
        var storeId = product?.definition?.storeSpecificId;

        DebugLog($"购买失败 ID: '{productId}', PurchaseFailureReason: {failureReason}");
        PurchaseResultHandler(productId, HQIapPurchaseResult.Failed);


        string scene = null;
        if (productId != null)
        {
            _prodictIdScene.TryGetValue(productId, out scene);
        }
        HQIapEvent.IapBuyFail(productId, storeId, scene, HQIapStatus.StoreError, failureReason.ToString());
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        DebugLog("OnPurchaseFailed 购买失败");

        var productId = product?.definition?.id;
        var storeId = product?.definition?.storeSpecificId;

        DebugLog($"购买失败 ID: '{product.definition.id}'," +
            $" Purchase failure reason: {failureDescription.reason}," +
            $" Purchase failure details: {failureDescription.message}");
        PurchaseResultHandler(product.definition.id, HQIapPurchaseResult.Failed);

        string scene = null;
        if (productId != null)
        {
            _prodictIdScene.TryGetValue(productId, out scene);
        }
        HQIapEvent.IapBuyFail(productId, storeId, scene, HQIapStatus.StoreError, failureDescription.reason.ToString());

    }

    void OnRestored(bool success, string error)
    {
        DebugLog("OnRestored");

        if (success)
        {
            _restoreResultHandler?.Invoke(HQIapPurchaseResult.Succeeded);
            HQIapEvent.IapRestoreSucc();
        }
        else
        {
            _restoreResultHandler?.Invoke(HQIapPurchaseResult.Failed);
            HQIapEvent.IapRestoreFail(HQIapStatus.StoreError, error);
        }
        _restoreResultHandler = null;

    }
    #endregion

    #region 日志

    private void DebugLog(string message)
    {
#if hq_log
        Log.Info("[HQIap] " + message);
#else
        Debug.Log("[HQIap] " + message);
#endif
    }

    private void DebugLogError(string message)
    {
#if hq_log
        Log.Error("[HQIap] " + message);
#else
        Debug.LogError("[HQIap]: " + message);
#endif
    }
    #endregion


    #region 工厂方法
    // private static readonly object _lock = new object();
    // private static HQIap _instance;

    // public static HQIap Instance
    // {
    //     get
    //     {
    //         lock (_lock)
    //         {
    //             return _instance != null ? _instance : _instance = new HQIap();
    //         }
    //     }
    // }

    protected static HQIap instance;
    // private static bool _onApplicationQuit;

    public static HQIap Instance
    {
        get
        {

            if (instance == null)
            {
                instance = FindObjectOfType<HQIap>();
                if (FindObjectsOfType<HQIap>().Length > 1)
                {
                    return instance;
                }

                if (instance == null)
                {
                    string instanceName = typeof(HQIap).Name;
                    GameObject instanceGO = GameObject.Find(instanceName);

                    if (instanceGO == null)
                    {
                        instanceGO = new GameObject(instanceName);
                    }

                    instance = instanceGO.AddComponent<HQIap>();
                    DontDestroyOnLoad(instanceGO); //保证实例不会被释放                 
                }
            }

            return instance;
        }
    }

    #endregion



}
