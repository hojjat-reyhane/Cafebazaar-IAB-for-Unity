using UnityEngine;
using SimpleJSON;
using System;
using UnityEngine.Networking;
using System.Collections;

public class StoreHandler : MonoBehaviour {

    public Product[] products;
    public string publicKey;
    public string payload;
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

    private AndroidJavaObject pluginUtilsClass = null;
    private bool isInitialized = false;
    private int selectedProductIndex;
    private Purchase currentPurchase;
    private Action<int, string> mErrorEvent;
    private Action<Purchase, int> mSuccesEvent;
    
    private static readonly string utilsClass = "ir.cafebazaar.iab.ServiceBillingBazaar";
    private static readonly string editorModeError = "You can't use In App Billing in Editor mode. It only works on Android devices.";
    
    public const int ERROR_WRONG_SETTINGS = 1;
    public const int ERROR_BAZAAR_NOT_INSTALLED = 2;
    public const int ERROR_SERVICE_NOT_INITIALIZED = 3;
    public const int ERROR_INTERNAL = 4;
    public const int ERROR_OPERATION_CANCELLED = 5;
    public const int ERROR_CONSUME_PURCHASE = 6;
    public const int ERROR_NOT_LOGGED_IN = 7;
    public const int ERROR_HAS_NOT_PRODUCT_IN_INVENTORY = 8;
    public const int ERROR_CONNECTING_VALIDATE_API = 9;
    public const int ERROR_PURCHASE_IS_REFUNDED = 10;
    public const int ERROR_NOT_SUPPORTED_IN_EDITOR = 11;
    
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

    private void Start()
    {
        publicKey = publicKey.Replace(" ", "");
        clientId = clientId.Replace(" ", "");
        clientSecret = clientSecret.Replace(" ", "");
        refreshToken = refreshToken.Replace(" ", "");
        InitializeBillingService();
    }

    public void InitializeBillingService()
    {
        if (IsBillingServiceInitialized) return;
        
        if (Application.platform == RuntimePlatform.Android)
        {
            using (AndroidJavaClass pluginClass = new AndroidJavaClass(utilsClass))
            {
                if (pluginClass != null)
                {
                    pluginUtilsClass = pluginClass.CallStatic<AndroidJavaObject>("GetInstance");
                    pluginUtilsClass.Call("SetPublicKey", publicKey);
                    pluginUtilsClass.Call("StartIabService");
                }
            }
        }
    }

    public void Purchase(int productIndex, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
    {
        mErrorEvent = errorEvent;
        mSuccesEvent = successEvent;
        selectedProductIndex = productIndex;

        if (Application.platform == RuntimePlatform.Android)
        {
            if (!IsBillingServiceInitialized)
            {
                InitializeBillingService();
                if (mErrorEvent != null)
                {
                    mErrorEvent.Invoke(CategorizeErrorCode(6), "Billing service is not initialized.");
                    mErrorEvent = null;
                }
                return;
            }

            if (pluginUtilsClass != null)
            {
                pluginUtilsClass.Call("Purchase", products[productIndex].productId, 
                    (products[productIndex].type == Product.ProductType.Consumable) && !validatePurchases, 
                    payload);
            }
        }
        else
        {
            Debug.LogError(editorModeError);
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(30), editorModeError);
                mErrorEvent = null;
            }
        }
    }

    public void CheckInventory(int productIndex, Action<int, string> errorEvent, Action<Purchase, int> successEvent)
    {
        mErrorEvent = errorEvent;
        mSuccesEvent = successEvent;
        selectedProductIndex = productIndex;

        if (Application.platform == RuntimePlatform.Android)
        {
            if (!IsBillingServiceInitialized)
            {
                InitializeBillingService();
                if (mErrorEvent != null)
                {
                    mErrorEvent.Invoke(CategorizeErrorCode(6), "Billing service is not initialized.");
                    mErrorEvent = null;
                }
                return;
            }

            if (pluginUtilsClass != null)
            {
                pluginUtilsClass.Call("CheckInventory", products[productIndex].productId);
            }
        }
        else
        {
            Debug.LogError(editorModeError);
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(30), editorModeError);
                mErrorEvent = null;
            }
        }
    }

    private void ConsumePurchase(Purchase purchase)
    {
        Debug.Log("Call ConsumePurchase()");

        if (pluginUtilsClass != null)
        {
            pluginUtilsClass.Call("ConsumePurchase", purchase.itemType, purchase.json, purchase.signature);
        }
    }

    private void ValidatePurchace(Purchase purchase)
    {
        Debug.Log("ValidatePurchace()");
        if (purchase == null)
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(40), "purchase is not valid.");
                mErrorEvent = null;
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
            StartCoroutine(GetRefreshCodeCoroutine(postRequest));
        }
        catch (Exception e)
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(41), "error requesting access code. " + e.StackTrace);
                mErrorEvent = null;
            }
        }
    }

    private IEnumerator GetRefreshCodeCoroutine(UnityWebRequest www)
    {
        Debug.Log("GetRefreshCodeCoroutine()");
        yield return www.SendWebRequest();

        if (www.isDone && !www.isNetworkError && !www.isHttpError)
        {
            string resultJSON = www.downloadHandler.text;
            JSONNode json = JSON.Parse(resultJSON);

            String accessToken = json["access_token"].Value.ToString();

            string checkURL = "https://pardakht.cafebazaar.ir/devapi/v2/api/validate/" + Application.identifier + "/inapp/"
                + currentPurchase.productId
                + "/purchases/" + currentPurchase.orderId
                + "/?access_token=" + accessToken;
            UnityWebRequest www1 = UnityWebRequest.Get(checkURL);
            StartCoroutine(ValidatePurchaseCoroutine(www1));
        }
        else
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(42), "error requesting access code.");
                mErrorEvent = null;
            }
        }
    }

    private IEnumerator ValidatePurchaseCoroutine(UnityWebRequest www)
    {
        Debug.Log("ValidatePurchaseCoroutine()");
        yield return www.SendWebRequest();

        if (www.isDone && !www.isNetworkError && !www.isHttpError)
        {
            string resultJSON = www.downloadHandler.text;

            Debug.Log("resultJSON: " + resultJSON);
            JSONNode json = JSON.Parse(resultJSON);

            ValidateResult result = new ValidateResult();
            result.isConsumed = json["consumptionState"].AsInt == 0;
            result.isRefund = json["purchaseState"].AsInt == 1;
            result.kind = json["kind"].Value.ToString();
            result.payload = json["developerPayload"].Value.ToString();
            result.time = json["purchaseTime"].Value.ToString();

            if (result.isRefund)
            {
                if (mErrorEvent != null)
                {
                    mErrorEvent.Invoke(CategorizeErrorCode(43), "purchase is refunded.");
                    mErrorEvent = null;
                }
            }
            else
            {
                if (products[selectedProductIndex].type == Product.ProductType.Consumable && !result.isConsumed)
                {
                    ConsumePurchase(currentPurchase);
                }
                else
                {
                    if (mSuccesEvent != null)
                    {
                        mSuccesEvent.Invoke(currentPurchase, selectedProductIndex);
                        mSuccesEvent = null;
                    }
                }
            }
        }
        else
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(44), "error validating purchase. " + www.error);
                mErrorEvent = null;
            }
        }
    }

    private AndroidJavaResult CheckAndroidJavaResult(string result)
    {
        if (result.Length == 0 || result == "" || result == null)
        {
            return new AndroidJavaResult(31, "unknown error!!!");
        }

        try
        {
            JSONNode purchaseResult = JSON.Parse(result);
            int errorCode = purchaseResult["errorCode"].AsInt;
            string data = purchaseResult["data"].Value.ToString();

            return new AndroidJavaResult(errorCode, data);
        }
        catch
        {
            return new AndroidJavaResult(32, "the result from cafeBazaar is not valid.");
        }
    }

    private Purchase ParsePurchaseData(string data)
    {
        //Sample JSON
        //{"orderId": "xxxxxxxxxx", "purchaseToken": "xxxxxxxxxx", "developerPayload": "YOUR_DEVELOPER_PAYLOAD", 
        //"packageName": "YOUR_PACKAGE_NAME", "purchaseState": 0, "purchaseTime": 1427814481707, "productId": "YOUR_PRODUCT_ID", 
        //"itemType": "inapp", "signature": "xxxxxxx"}

        JSONNode info = JSONNode.Parse(data);
        Purchase purchase = new Purchase();
        purchase.orderId = info["orderId"].Value.ToString();
        purchase.purchaseToken = info["purchaseToken"].Value.ToString();
        purchase.payload = info["developerPayload"].Value.ToString();
        purchase.packageName = info["packageName"].Value.ToString();
        purchase.purchaseState = info["purchaseState"].AsInt;
        purchase.purchaseTime = info["purchaseTime"].Value.ToString();
        purchase.productId = info["productId"].Value.ToString();
        purchase.itemType = info["itemType"].Value.ToString();
        purchase.signature = info["signature"].Value.ToString();
        purchase.json = data;
        return purchase;
    }
    
    private void OnApplicationQuit()
    {
        if (pluginUtilsClass != null)
        {
            pluginUtilsClass.Call("StopIabHelper");
        }
    }
    
    #region Android Java Interactions
    public void GetPurchaseResult(string result)
    {
        Debug.Log("GetPurchaseResult(): " + result);
        AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
        if (iabResult.errorCode == 0)
        {
            if (validatePurchases)
            {
                ValidatePurchace(ParsePurchaseData(iabResult.data));
            }
            else
            {
                if (products[selectedProductIndex].type == Product.ProductType.Consumable)
                {
                    ConsumePurchase(ParsePurchaseData(iabResult.data));
                }
                else
                {
                    if (mSuccesEvent != null)
                    {
                        mSuccesEvent.Invoke(ParsePurchaseData(iabResult.data), selectedProductIndex);
                        mSuccesEvent = null;
                    }
                }
            }
        }
        else
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(iabResult.errorCode), iabResult.data);
                mErrorEvent = null;
            }
        }
    }

    public void GetConsumeResult(string result)
    {
        Debug.Log("GetConsumeResult(): " + result);
        AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
        if (iabResult.errorCode == 0)
        {
            if (mSuccesEvent != null)
            {
                mSuccesEvent.Invoke(ParsePurchaseData(iabResult.data), selectedProductIndex);
                mSuccesEvent = null;
            }
        }
        else
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(iabResult.errorCode), iabResult.data);
                mErrorEvent = null;
            }
        }
    }

    public void GetInventoryResult(string result)
    {
        AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
        if (iabResult.errorCode == 0)
        {
            if (validatePurchases)
            {
                ValidatePurchace(ParsePurchaseData(iabResult.data));
            }
            else
            {
                if (mSuccesEvent != null)
                {
                    mSuccesEvent.Invoke(ParsePurchaseData(iabResult.data), selectedProductIndex);
                    mSuccesEvent = null;
                }
            }
        }
        else
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(iabResult.errorCode), iabResult.data);
                mErrorEvent = null;
            }
        }
    }

    public void GetInitializeResult(string result)
    {
        AndroidJavaResult iabResult = CheckAndroidJavaResult(result);
        if (iabResult.errorCode == 0)
        {
            isInitialized = true;
        }
        else
        {
            if (mErrorEvent != null)
            {
                mErrorEvent.Invoke(CategorizeErrorCode(iabResult.errorCode), iabResult.data);
                mErrorEvent = null;
            }
        }
    }

    public void DebugLog(string msg)
    {
        Debug.Log(msg);
    }
    #endregion

    private int CategorizeErrorCode(int errorCode)
    {
        switch (errorCode)
        {
            case 1:
                return ERROR_WRONG_SETTINGS;
            case 2:
                return ERROR_BAZAAR_NOT_INSTALLED;
            case 3:
            case 4:
            case 5:
            case 6:
                return ERROR_SERVICE_NOT_INITIALIZED;
            case 21:
            case 22:
                return ERROR_OPERATION_CANCELLED;
            case 11:
            case 17:
            case 18:
            case 23:
                return ERROR_CONSUME_PURCHASE;
            case 24:
                return ERROR_NOT_LOGGED_IN;
            case 26:
                return ERROR_HAS_NOT_PRODUCT_IN_INVENTORY;
            case 30:
                return ERROR_NOT_SUPPORTED_IN_EDITOR;
            case 40:
            case 41:
            case 42:
            case 44:
                return ERROR_CONNECTING_VALIDATE_API;
            case 43:
                return ERROR_PURCHASE_IS_REFUNDED;
            default:
                return ERROR_INTERNAL;
        }
    }

    private class AndroidJavaResult
    {
        public int errorCode;
        public string data;

        public AndroidJavaResult(int mErrorCode, string mData)
        {
            errorCode = mErrorCode;
            data = mData;
        }
    }
}

[Serializable]
public class Product
{
    public enum ProductType { Consumable, NonConsumable };
    public string productId;
    public ProductType type;
    public int price;
}

public class Purchase
{
    public string orderId;
    public string purchaseToken;
    public string payload;
    public string packageName;
    public int purchaseState;
    public string purchaseTime;
    public string productId;
    public string itemType;
    public string signature;
    public string json;
}

public class ValidateResult
{
    public bool isConsumed;
    public bool isRefund;
    public string kind;
    public string payload;
    public string time;
}

