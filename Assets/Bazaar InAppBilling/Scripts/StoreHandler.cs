using UnityEngine;
using System;
using UnityEngine.Networking;
using System.Collections;

namespace BazaarInAppBilling
{
    /// <summary>
    ///     Handles interactions with Bazaar IAB Java library and provides convenience methods for in-app billing.
    ///     You can use it to process in-app billing operations.
    /// 
    ///     Define your products informations, public key and other settings in your scene.
    ///     Note that this information is sensitive and any mismatching with Bazaar developer's panel can cause problems.
    /// 
    ///     Only Android devices can run in-app billing operations. So you can't test purchasing in the editor mode.
    /// 
    ///     You must perform initialization in order to start using it.
    ///     To perform initialization, call the <see cref="InitializeBillingService"/> method and provide event functions.
    ///     Success event will be called when initialization is complete, after which (and not before) you may call other methods.
    ///     
    ///     After initialization is complete, you will typically want to request products prices from Bazaar. 
    ///     See <see cref="LoadProductPrices"/>
    ///     
    ///     Author: Hojjat.Reyhane
    /// </summary>
    public class StoreHandler : MonoBehaviour
    {
        public Product[] products;
        public string publicKey;
        public string payload;
        public bool editorDummyResponse = false;
        public bool validatePurchases = true;
        public string clientId;
        public string clientSecret;
        public string refreshToken;

        public static StoreHandler instance;

        public bool IsBillingServiceInitialized
        {
            get
            {
                return isInitialized;
            }
        }

        public bool IsProductsPricesLoaded
        {
            get
            {
                return isPricesLoaded;
            }
        }

        /// <summary>
        /// Wrong settings provided in the Unity Editor. Enter public key (RSA from Bazaar panel) in the StoreHandler component.
        /// </summary>
        public const int ERROR_WRONG_SETTINGS = 1;

        /// <summary>
        /// User didn't install CafeBazaar application.
        /// </summary>
        public const int ERROR_BAZAAR_NOT_INSTALLED = 2;

        /// <summary>
        /// Billing service isn't initialized. Encorage user to retry.
        /// </summary>
        public const int ERROR_SERVICE_NOT_INITIALIZED = 3;

        /// <summary>
        /// Internal error happened. Follow the instructions carefully and if it persists contact Bazaar developer support.
        /// </summary>
        public const int ERROR_INTERNAL = 4;

        /// <summary>
        /// Operation cancelled by user or failed.
        /// </summary>
        public const int ERROR_OPERATION_CANCELLED = 5;

        /// <summary>
        /// Purchase was successful but couldn't consume it. So the purchase is stored in user's inventory and can be restore later without paying.
        /// </summary>
        public const int ERROR_CONSUME_PURCHASE = 6;

        /// <summary>
        /// User is not logged in to Cafebazaar application so can't check the inventory.
        /// </summary>
        public const int ERROR_NOT_LOGGED_IN = 7;

        /// <summary>
        /// User has not this product in the inventory or the product is consumed.
        /// </summary>
        public const int ERROR_HAS_NOT_PRODUCT_IN_INVENTORY = 8;

        /// <summary>
        /// Couldn't connect validating API due to internet connection failure or wrong client info in StoreHandler.cs.
        /// </summary>
        public const int ERROR_CONNECTING_VALIDATE_API = 9;

        /// <summary>
        /// Purchase is refunded
        /// </summary>
        public const int ERROR_PURCHASE_IS_REFUNDED = 10;

        /// <summary>
        /// You can't use In App Billing in Editor mode. It only works on Android devices.
        /// </summary>
        public const int ERROR_NOT_SUPPORTED_IN_EDITOR = 11;

        /// <summary>
        /// Product index is not valid and doesn't exist in products[] array.
        /// </summary>
        public const int ERROR_WRONG_PRODUCT_INDEX = 12;

        /// <summary>
        /// Invalid Id exists in products[] array or no product is defined.
        /// </summary>
        public const int ERROR_WRONG_PRODUCT_ID = 13;

        /// <summary>
        /// Billing service initialized, retry the operation to purchase.
        /// </summary>
        public const int SERVICE_IS_NOW_READY_RETRY_OPERATION = 14;

        private AndroidJavaObject pluginUtilsClass = null;
        private bool isInitializing = false;
        private bool isInitialized = false;
        private bool isPricesLoaded = false;
        private bool isCheckingInventory = false;
        private int selectedProductIndex;
        private Purchase currentPurchase;
        private Action<int, string> mInitializeErrorEvent;
        private Action<int, string> mDetailErrorEvent;
        private Action<int, string> mPurchaseErrorEvent;
        private Action<int, string> mConsumeErrorEvent;
        private Action<int, string> mInventoryErrorEvent;
        private Action<Purchase, int> mPurchaseSuccessEvent;
        private Action<Purchase, int> mConsumeSuccessEvent;
        private Action<Purchase, int> mInventorySuccessEvent;
        private Action mInitializeSuccessEvent;
        private Action mDetailSuccessEvent;

        private static readonly string utilsClass = "ir.cafebazaar.iab.ServiceBillingBazaar";
        private static readonly string editorModeError = "You can't use In App Billing in Editor mode. It only works on Android devices.";

        private static string[] persianNumbers = { "۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹" };

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Initializes Bazaar billing service. If any problem occurs, the errorEvent function will be called.
        /// If everything goes well, the successEvent will be called.
        /// </summary>
        /// <param name="errorEvent">The function that will be called after initialization failure.
        /// Has an integer parameter for error code and a string parameter for error message.
        /// This function is like "void OnServiceInitializationFailed(int errorCode, string message){}"</param>
        /// <param name="successEvent">The function that will be called after successful initialization.
        /// This function is like "void OnServiceInitializedSuccessfully(){}"</param>
        public void InitializeBillingService(Action<int, string> errorEvent, Action successEvent)
        {
            mInitializeErrorEvent = errorEvent;
            mInitializeSuccessEvent = successEvent;

            if (IsBillingServiceInitialized)
            {
                if (mInitializeSuccessEvent != null)
                {
                    mInitializeSuccessEvent.Invoke();
                    mInitializeSuccessEvent = null;
                }
                else if (mInitializeErrorEvent != null)
                {
                    mInitializeErrorEvent.Invoke(SERVICE_IS_NOW_READY_RETRY_OPERATION, "Service is initialized");
                    mInitializeErrorEvent = null;
                }
                return;
            }

            if (isInitializing) return;

            if (!IsAndroidPlayer(mInitializeErrorEvent))
            {
                if (editorDummyResponse)
                {
                    isInitialized = true;
                    if (mInitializeSuccessEvent != null)
                    {
                        mInitializeSuccessEvent.Invoke();
                        mInitializeSuccessEvent = null;
                    }
                    else if (mInitializeErrorEvent != null)
                    {
                        mInitializeErrorEvent.Invoke(SERVICE_IS_NOW_READY_RETRY_OPERATION, "Service is initialized");
                        mInitializeErrorEvent = null;
                    }
                }
                return;
            }

            using (AndroidJavaClass pluginClass = new AndroidJavaClass(utilsClass))
            {
                if (pluginClass != null)
                {
                    pluginUtilsClass = pluginClass.CallStatic<AndroidJavaObject>("GetInstance");
                    pluginUtilsClass.Call("SetPublicKey", publicKey);
                    pluginUtilsClass.Call("SetCallbackGameObject", gameObject.name);
                    pluginUtilsClass.Call("StartIabService");
                    isInitializing = true;
                }
            }
        }

        /// <summary>
        /// Initializes Bazaar billing service.
        /// </summary>
        public void InitializeBillingService()
        {
            InitializeBillingService(null, null);
        }

        /// <summary>
        /// Loads products prices from Bazaar. If any problem occurs, the errorEvent function will be called.
        /// If everything goes well, the successEvent will be called.
        /// </summary>
        /// <param name="errorEvent">The function that will be called after loading prices failure.
        /// Has an integer parameter for error code and a string parameter for error message.
        /// This function is like "void OnPricesLoadFailed(int errorCode, string message){}"</param>
        /// <param name="successEvent">The function that will be called after successfully loading products prices.
        /// This function is like "void OnPricesLoadedSuccessfully(){}"</param>
        public void LoadProductPrices(Action<int, string> errorEvent, Action successEvent)
        {
            mDetailErrorEvent = errorEvent;
            mDetailSuccessEvent = successEvent;

            if (!IsBillingServiceInitialized)
            {
                InitializeBillingService(mDetailErrorEvent, null);
                return;
            }

            if (!IsAndroidPlayer(mDetailErrorEvent))
            {
                if (editorDummyResponse)
                {
                    if (mDetailSuccessEvent != null)
                    {
                        mDetailSuccessEvent.Invoke();
                        mDetailSuccessEvent = null;
                    }
                }
                return;
            }

            if (pluginUtilsClass != null)
            {
                if (products.Length > 0)
                {
                    string productIds = products[0].productId;
                    for (int i = 1; i < products.Length; i++) productIds += ("," + products[i].productId);
                    pluginUtilsClass.Call("GetProductsDetails", productIds);
                }
                else
                {
                    Debug.LogError("No product found in products[] array");
                    if (mDetailErrorEvent != null)
                    {
                        mDetailErrorEvent.Invoke(ERROR_WRONG_PRODUCT_ID, "No product found in products[] array");
                        mDetailErrorEvent = null;
                    }
                }
            }
        }

        /// <summary>
        /// Initiate the Bazaar flow for an in-app purchase. Call this method to initiate an in-app purchase,
        /// which will involve bringing up the Bazaar screen. The game will be paused while
        /// the user interacts with Bazaar, and the result will be delivered via errorEvent or successEvent.
        /// </summary>
        /// <param name="productIndex">Index of the product from products[] array to purchase</param>
        /// <param name="errorEvent">The function that will be called after purchase failure.
        /// Has an integer parameter for error code and a string parameter for error message.
        /// This function is like "void OnPurchaseFailed(int errorCode, string message){}"</param>
        /// <param name="successEvent">The function that will be called after successful purchase.
        /// Has a purchase parameter wich you can get purchase details from it and an integer parameter as purchased product index.
        /// This function is like "void OnPurchasedSuccessfully(Purchase purchase, int productIndex){}"</param>
        public void Purchase(int productIndex, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
        {
            mPurchaseErrorEvent = errorEvent;
            mPurchaseSuccessEvent = successEvent;
            selectedProductIndex = productIndex;

            if (!IsProductIndexValid(productIndex, mPurchaseErrorEvent)) return;

            if (!IsBillingServiceInitialized)
            {
                InitializeBillingService(mPurchaseErrorEvent, null);
                return;
            }

            if (!IsAndroidPlayer(mPurchaseErrorEvent))
            {
                if (editorDummyResponse)
                {
                    if (mPurchaseSuccessEvent != null)
                    {
                        mPurchaseSuccessEvent.Invoke(new Purchase("test", products[selectedProductIndex].productId), selectedProductIndex);
                        mPurchaseSuccessEvent = null;
                    }
                }
                return;
            }

            if (pluginUtilsClass != null)
            {
                bool consumeImmidiate = false;
                if((products[productIndex].type == Product.ProductType.Consumable) && !validatePurchases)
                {
                    consumeImmidiate = true;
                    mConsumeErrorEvent = mPurchaseErrorEvent;
                    mConsumeSuccessEvent = mPurchaseSuccessEvent;
                }

                pluginUtilsClass.Call("Purchase", products[productIndex].productId, consumeImmidiate, payload);
            }
        }

        /// <summary>
        /// Checks if user has this particular product in the inventory. If any problem occurs or user hasn't the product, the errorEvent function will be called.
        /// If user purchased the product and it's not consumed, the successEvent will be called.
        /// </summary>
        /// <param name="productIndex">Index of the product from products[] array to check</param>
        /// <param name="errorEvent">The function that will be called after failure or not having the product in the inventory.
        /// Has an integer parameter for error code and a string parameter for error message.
        /// This function is like "void OnInventoryCheckFailed(int errorCode, string message){}"</param>
        /// <param name="successEvent">The function that will be called after confirming that user has the product.
        /// Has a purchase parameter wich you can get purchase details from it and an integer parameter as purchased product index.
        /// This function is like "void OnInventoryHadProduct(Purchase purchase, int productIndex){}"</param>
        public void CheckInventory(int productIndex, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
        {
            mInventoryErrorEvent = errorEvent;
            mInventorySuccessEvent = successEvent;
            selectedProductIndex = productIndex;

            if (!IsProductIndexValid(productIndex, mInventoryErrorEvent)) return;

            if (!IsBillingServiceInitialized)
            {
                InitializeBillingService(mInventoryErrorEvent, null);
                return;
            }

            if (!IsAndroidPlayer(mInventoryErrorEvent))
            {
                if (editorDummyResponse)
                {
                    if (mInventorySuccessEvent != null)
                    {
                        mInventorySuccessEvent.Invoke(new Purchase("test", products[selectedProductIndex].productId), selectedProductIndex);
                        mInventorySuccessEvent = null;
                    }
                }
                return;
            }

            if (pluginUtilsClass != null)
            {
                pluginUtilsClass.Call("CheckInventory", products[productIndex].productId);
            }
        }

        /// <summary>
        /// Consumes a given in-app product. Consuming can only be done on an item
        /// that's owned, and as a result of consumption, the user will no longer own it.
        /// If any problem occurs, the errorEvent function will be called.
        /// If everything goes well, the successEvent will be called.
        /// </summary>
        /// <param name="purchase">The purchase info that represents the item to consume.</param>
        /// <param name="productIndex">Index of the product from products[] array to consume</param>
        /// <param name="errorEvent">The function that will be called after cunsumption failure.
        /// Has an integer parameter for error code and a string parameter for error message.
        /// This function is like "void OnConsumptionFailed(int errorCode, string message){}"</param>
        /// <param name="successEvent">The function that will be called after successful cunsumption.
        /// Has a purchase parameter wich you can get purchase details from it and an integer parameter as purchased product index.
        /// This function is like "void OnConsumedSuccessfully(Purchase purchase, int productIndex){}"</param>
        public void ConsumePurchase(Purchase purchase, int productIndex, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
        {
            mConsumeErrorEvent = errorEvent;
            mConsumeSuccessEvent = successEvent;
            selectedProductIndex = productIndex;

            if (!IsProductIndexValid(productIndex, mConsumeErrorEvent)) return;

            if (!IsBillingServiceInitialized)
            {
                InitializeBillingService(mConsumeErrorEvent, null);
                return;
            }

            if (!IsAndroidPlayer(mConsumeErrorEvent))
            {
                if (editorDummyResponse)
                {
                    if (mConsumeSuccessEvent != null)
                    {
                        mConsumeSuccessEvent.Invoke(new Purchase("test", products[selectedProductIndex].productId), selectedProductIndex);
                        mConsumeSuccessEvent = null;
                    }
                }
                return;
            }

            if (pluginUtilsClass != null)
            {
                pluginUtilsClass.Call("ConsumePurchase", purchase.itemType, purchase.json, purchase.signature);
            }
        }

        private bool IsProductIndexValid(int productIndex, Action<int, string> errorEvent)
        {
            if (productIndex < 0 || productIndex >= products.Length)
            {
                if (errorEvent != null)
                {
                    errorEvent.Invoke(ERROR_WRONG_PRODUCT_INDEX, "Wrong product index while consuming purchase. index: " + productIndex);
                    errorEvent = null;
                }

                return false;
            }

            return true;
        }

        private bool IsAndroidPlayer(Action<int, string> errorEvent)
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                return true;
            }
            else
            {
                Debug.LogError(editorModeError);
                if (!editorDummyResponse)
                {
                    if (errorEvent != null)
                    {
                        errorEvent.Invoke(ERROR_NOT_SUPPORTED_IN_EDITOR, editorModeError);
                        errorEvent = null;
                    }
                }
                return false;
            }
        }

        private void ValidatePurchace(Purchase purchase, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
        {
            if (purchase == null)
            {
                if (errorEvent != null)
                {
                    errorEvent.Invoke(ERROR_CONNECTING_VALIDATE_API, "purchase is not valid.");
                    errorEvent = null;
                }
                return;
            }

            currentPurchase = purchase;

            try
            {
                WWWForm form = new WWWForm();
                form.AddField("grant_type", "refresh_token");
                form.AddField("client_id", clientId);
                form.AddField("client_secret", clientSecret);
                form.AddField("refresh_token", refreshToken);
                UnityWebRequest postRequest = UnityWebRequest.Post("https://pardakht.cafebazaar.ir/devapi/v2/auth/token/", form);
                StartCoroutine(GetRefreshCodeCoroutine(postRequest, errorEvent, successEvent));
            }
            catch (Exception e)
            {
                if (errorEvent != null)
                {
                    errorEvent.Invoke(ERROR_CONNECTING_VALIDATE_API, "error requesting access code. " + e.StackTrace);
                    errorEvent = null;
                }
            }
        }

        private IEnumerator GetRefreshCodeCoroutine(UnityWebRequest www, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
        {
            yield return www.SendWebRequest();

            if (www.isDone && !www.isNetworkError && !www.isHttpError)
            {
                string resultJSON = www.downloadHandler.text;
                APIToken token = JsonUtility.FromJson<APIToken>(resultJSON);

                string checkURL = "https://pardakht.cafebazaar.ir/devapi/v2/api/validate/" + Application.identifier + "/inapp/"
                    + currentPurchase.productId
                    + "/purchases/" + currentPurchase.orderId
                    + "/?access_token=" + token.access_token;
                UnityWebRequest www1 = UnityWebRequest.Get(checkURL);
                StartCoroutine(ValidatePurchaseCoroutine(www1, errorEvent, successEvent));
            }
            else
            {
                if (errorEvent != null)
                {
                    errorEvent.Invoke(ERROR_CONNECTING_VALIDATE_API, "error requesting access code.");
                    errorEvent = null;
                }
            }
        }

        private IEnumerator ValidatePurchaseCoroutine(UnityWebRequest www, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
        {
            yield return www.SendWebRequest();

            if (www.isDone && !www.isNetworkError && !www.isHttpError)
            {
                string resultJSON = www.downloadHandler.text;
                ValidateResult result = JsonUtility.FromJson<ValidateResult>(resultJSON);

                if (result.isRefund)
                {
                    if (errorEvent != null)
                    {
                        errorEvent.Invoke(ERROR_PURCHASE_IS_REFUNDED, "purchase is refunded.");
                        errorEvent = null;
                    }
                }
                else
                {
                    if (!isCheckingInventory && products[selectedProductIndex].type == Product.ProductType.Consumable && !result.isConsumed)
                    {
                        ConsumePurchase(currentPurchase, selectedProductIndex, errorEvent, successEvent);
                    }
                    else
                    {
                        if (successEvent != null)
                        {
                            successEvent.Invoke(currentPurchase, selectedProductIndex);
                            successEvent = null;
                        }
                    }
                }
            }
            else
            {
                if (errorEvent != null)
                {
                    errorEvent.Invoke(ERROR_CONNECTING_VALIDATE_API, "error validating purchase. " + www.error);
                    errorEvent = null;
                }
            }
        }

        private AndroidJavaResult CheckAndroidJavaResult(string result)
        {
            if (result.Length == 0 || result == "" || result == null)
            {
                return new AndroidJavaResult(ERROR_INTERNAL, "unknown error!!!");
            }

            try
            {
                return JsonUtility.FromJson<AndroidJavaResult>(result);
            }
            catch
            {
                return new AndroidJavaResult(ERROR_INTERNAL, "the result from cafeBazaar is not valid.");
            }
        }

        private Purchase ParsePurchaseData(string data)
        {
            Purchase purchase = JsonUtility.FromJson<Purchase>(data);
            purchase.json = data;
            return purchase;
        }

        private void ParseProductsPrices(string data)
        {
            ProductsDetailsArray array = JsonUtility.FromJson<ProductsDetailsArray>("{\"details\" : " + data + "}");
            for (int i = 0; i < array.details.Length; i++)
            {
                if (products[i].productId == array.details[i].productId)
                {
                    products[i].price = GetNumericPrice(array.details[i].price);
                }
            }
        }

        private string GetNumericPrice(string text)
        {
            string price = text.Replace("ریال", "");
            price = price.Replace(" ", "");
            price = price.Replace("٬", "");
            price = price.Replace("صفر", "0");
            for (int i = 0; i < persianNumbers.Length; i++)
            {
                price = price.Replace(persianNumbers[i], i + "");
            }

            return price;
        }

        private void OnValidate()
        {
            publicKey = publicKey.Replace(" ", "");
            clientId = clientId.Replace(" ", "");
            clientSecret = clientSecret.Replace(" ", "");
            refreshToken = refreshToken.Replace(" ", "");
        }

        private void OnApplicationQuit()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                if (IsBillingServiceInitialized)
                {
                    if (pluginUtilsClass != null)
                    {
                        pluginUtilsClass.Call("StopIabHelper");
                    }
                }
            }
        }

        #region Android Java Interactions
        /// <summary>
        /// Handles purchase result from Bazaar IAB Jar library.
        /// Don't change its name or make it private.
        /// </summary>
        public void JNI_PurchaseResult(string result)
        {
            AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
            if (iabResult.errorCode == 0)
            {
                if (validatePurchases)
                {
                    isCheckingInventory = false;
                    ValidatePurchace(ParsePurchaseData(iabResult.data), mPurchaseErrorEvent, mPurchaseSuccessEvent);
                }
                else
                {
                    if (products[selectedProductIndex].type == Product.ProductType.Consumable)
                    {
                        ConsumePurchase(ParsePurchaseData(iabResult.data), selectedProductIndex, mPurchaseErrorEvent, mPurchaseSuccessEvent);
                    }
                    else
                    {
                        if (mPurchaseSuccessEvent != null)
                        {
                            mPurchaseSuccessEvent.Invoke(ParsePurchaseData(iabResult.data), selectedProductIndex);
                            mPurchaseSuccessEvent = null;
                        }
                    }
                }
            }
            else
            {
                if (mPurchaseErrorEvent != null)
                {
                    mPurchaseErrorEvent.Invoke(iabResult.errorCode, iabResult.data);
                    mPurchaseErrorEvent = null;
                }
            }
        }

        /// <summary>
        /// Handles consumption result from Bazaar IAB Jar library.
        /// Don't change its name or make it private.
        /// </summary>
        public void JNI_ConsumeResult(string result)
        {
            AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
            if (iabResult.errorCode == 0)
            {
                if (mConsumeSuccessEvent != null)
                {
                    mConsumeSuccessEvent.Invoke(ParsePurchaseData(iabResult.data), selectedProductIndex);
                    mConsumeSuccessEvent = null;
                }
            }
            else
            {
                if (mConsumeErrorEvent != null)
                {
                    mConsumeErrorEvent.Invoke(iabResult.errorCode, iabResult.data);
                    mConsumeErrorEvent = null;
                }
            }
        }

        /// <summary>
        /// Handles inventory check result from Bazaar IAB Jar library.
        /// Don't change its name or make it private.
        /// </summary>
        public void JNI_InventoryResult(string result)
        {
            AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
            if (iabResult.errorCode == 0)
            {
                if (validatePurchases)
                {
                    isCheckingInventory = true;
                    ValidatePurchace(ParsePurchaseData(iabResult.data), mInventoryErrorEvent, mInventorySuccessEvent);
                }
                else
                {
                    if (mInventorySuccessEvent != null)
                    {
                        mInventorySuccessEvent.Invoke(ParsePurchaseData(iabResult.data), selectedProductIndex);
                        mInventorySuccessEvent = null;
                    }
                }
            }
            else
            {
                if (mInventoryErrorEvent != null)
                {
                    mInventoryErrorEvent.Invoke(iabResult.errorCode, iabResult.data);
                    mInventoryErrorEvent = null;
                }
            }
        }

        /// <summary>
        /// Handles requesting products prices result from Bazaar IAB Jar library.
        /// Don't change its name or make it private.
        /// </summary>
        public void JNI_ProductsDetailsResult(string result)
        {
            AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
            if (iabResult.errorCode == 0)
            {
                ParseProductsPrices(iabResult.data);
                isPricesLoaded = true;
                if (mDetailSuccessEvent != null)
                {
                    mDetailSuccessEvent.Invoke();
                    mDetailSuccessEvent = null;
                }
            }
            else
            {
                if (mDetailErrorEvent != null)
                {
                    mDetailErrorEvent.Invoke(iabResult.errorCode, iabResult.data);
                    mDetailErrorEvent = null;
                }
            }
        }

        /// <summary>
        /// Handles initialization result from Bazaar IAB Jar library.
        /// Don't change its name or make it private.
        /// </summary>
        public void JNI_InitializeResult(string result)
        {
            isInitializing = false;
            AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
            if (iabResult.errorCode == 0)
            {
                isInitialized = true;
                if (mInitializeSuccessEvent != null)
                {
                    mInitializeSuccessEvent.Invoke();
                    mInitializeSuccessEvent = null;
                }
                else if (mInitializeErrorEvent != null)
                {
                    mInitializeErrorEvent.Invoke(SERVICE_IS_NOW_READY_RETRY_OPERATION, iabResult.data);
                    mInitializeErrorEvent = null;
                }
            }
            else
            {
                if (mInitializeErrorEvent != null)
                {
                    mInitializeErrorEvent.Invoke(iabResult.errorCode, iabResult.data);
                    mInitializeErrorEvent = null;
                }
            }
        }

        /// <summary>
        /// Shows a log message from Bazaar IAB Jar library.
        /// Don't change its name or make it private.
        /// </summary>
        public void JNI_DebugLog(string msg)
        {
            Debug.Log(msg);
        }
        #endregion

        [Serializable]
        struct AndroidJavaResult
        {
            public int errorCode;
            public string data;

            public AndroidJavaResult(int mErrorCode, string mData)
            {
                errorCode = mErrorCode;
                data = mData;
            }
        }

        [Serializable]
        struct ValidateResult
        {
            public bool isConsumed;
            public bool isRefund;
            public string kind;
            public string payload;
            public string time;
        }

        [Serializable]
        struct ProductDetail
        {
            public string productId;
            public string price;
        }

        [Serializable]
        struct ProductsDetailsArray
        {
            public ProductDetail[] details;
        }

        [Serializable]
        struct APIToken
        {
            public string access_token;
        }
    }

    [Serializable]
    public class Product
    {
        public enum ProductType { Consumable, NonConsumable };
        public string productId;
        public ProductType type;
        public string price;
    }

    [Serializable]
    public class Purchase
    {
        public string orderId;
        public string purchaseToken;
        public string developerPayload;
        public string packageName;
        public int purchaseState;
        public string purchaseTime;
        public string productId;
        public string itemType;
        public string signature;
        [NonSerialized] public string json;

        public Purchase() { }

        public Purchase(string orderId, string productId)
        {
            this.orderId = orderId;
            this.productId = productId;
        }
    }
}