using BazaarInAppBilling;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InAppStore : MonoBehaviour
{
    public GameObject successDialog, errorDialog, loadingDialog;
    public Text txtResult;
    public Text txtCoins;
    public Text[] priceTexts;
    public Button btnDoubleCoin;
    
    private bool doubleCoin = false;
    private readonly string COIN_KEY = "coin";
    private readonly string DOUBLE_COIN_KEY = "doubleCoin";
    private int selectedProductIndex;

    void Start()
    {
        txtResult.text = "Initializing Billing Service ...\n" + txtResult.text;
        StoreHandler.instance.InitializeBillingService(OnServiceInitializationFailed, OnServiceInitializedSuccessfully);

        txtCoins.text = GetCoins() + "$";
        doubleCoin = PlayerPrefs.GetInt(DOUBLE_COIN_KEY, 0) == 1;
        btnDoubleCoin.interactable = !doubleCoin;
    }

    public void GetFreeCoins(int value)
    {
        if (doubleCoin) value *= 2;
        AddCoin(value);
    }

    public void BuyProduct(int index)
    {
        selectedProductIndex = index;
        loadingDialog.SetActive(true);
        StoreHandler.instance.Purchase(index, OnPurchaseFailed, OnPurchasedSuccessfully);
    }

    public void CheckInventory(int index)
    {
        selectedProductIndex = index;
        loadingDialog.SetActive(true);
        StoreHandler.instance.CheckInventory(index, OnInventoryCheckFailed, OnInventoryHadProduct);
    }

    public void SetValidatePurchasesState(bool state)
    {
        StoreHandler.instance.validatePurchases = state;
    }

    private void OnServiceInitializedSuccessfully()
    {
        txtResult.text = "Service Initialized.\n" + txtResult.text;
        txtResult.text = "Loading Product Prices ...\n" + txtResult.text;
        StoreHandler.instance.LoadProductPrices(OnLoadingPricesFailed, OnPricesLoadedSuccessfully);
    }

    private void OnServiceInitializationFailed(int errorCode, string message)
    {
        txtResult.text = "Initialization Failed. ErrorCode: " + errorCode + ", " + message + "\n" + txtResult.text;
    }
    
    private void OnPricesLoadedSuccessfully()
    {
        txtResult.text = "Products Prices Loaded.\n" + txtResult.text;
        for(int i = 0; i < StoreHandler.instance.products.Length; i++)
        {
            string price = StoreHandler.instance.products[i].price;
            if (price.Length == 0) price = "0";
            priceTexts[i].text = string.Format("{0:0,0}", int.Parse(price)) + " Rials";
        }
    }

    private void OnLoadingPricesFailed(int errorCode, string message)
    {
        txtResult.text = "Loading Prices Failed. ErrorCode: " + errorCode + ", " + message + "\n" + txtResult.text;
    }

    private void OnPurchasedSuccessfully(Purchase purchase, int productIndex)
    {
        loadingDialog.SetActive(false);
        successDialog.SetActive(true);
        txtResult.text = "The product: " + purchase.productId + " purchased successfully.\nToken: " + purchase.orderId + "\n" + txtResult.text;

        switch (productIndex)
        {
            case 0: // 500 coin
                AddCoin(500);
                break;
            case 1: // enable double coin
                EnableDoubleCoin();
                break;
            default:
                throw new UnassignedReferenceException("You forgot to give user the product after purchase. product: " + purchase.productId + ", index: " + productIndex);
        }
    }

    private void OnPurchaseFailed(int errorCode, string message)
    {
        loadingDialog.SetActive(false);
        txtResult.text = "ErrorCode: " + errorCode + ", " + message + "\n" + txtResult.text;

        switch (errorCode)
        {
            case StoreHandler.SERVICE_IS_NOW_READY_RETRY_OPERATION:

                BuyProduct(selectedProductIndex);

                return;
            case StoreHandler.ERROR_WRONG_SETTINGS:

                break;
            case StoreHandler.ERROR_BAZAAR_NOT_INSTALLED:

                break;
            case StoreHandler.ERROR_SERVICE_NOT_INITIALIZED:

                break;
            case StoreHandler.ERROR_INTERNAL:

                break;
            case StoreHandler.ERROR_OPERATION_CANCELLED:

                break;
            case StoreHandler.ERROR_CONSUME_PURCHASE:

                break;
            case StoreHandler.ERROR_NOT_LOGGED_IN:

                break;
            case StoreHandler.ERROR_HAS_NOT_PRODUCT_IN_INVENTORY:

                break;
            case StoreHandler.ERROR_CONNECTING_VALIDATE_API:

                break;
            case StoreHandler.ERROR_PURCHASE_IS_REFUNDED:

                break;
            case StoreHandler.ERROR_NOT_SUPPORTED_IN_EDITOR:

                break;
            case StoreHandler.ERROR_WRONG_PRODUCT_INDEX:

                break;
            case StoreHandler.ERROR_WRONG_PRODUCT_ID:

                break;
        }

        errorDialog.SetActive(true);
    }

    private void OnInventoryHadProduct(Purchase purchase, int productIndex)
    {
        loadingDialog.SetActive(false);
        successDialog.SetActive(true);
        txtResult.text = "You had " + purchase.productId + " in your inventory.\n" + txtResult.text;

        if (productIndex == 1)
        {
            EnableDoubleCoin();
        }
    }

    private void OnInventoryCheckFailed(int errorCode, string message)
    {
        switch (errorCode)
        {
            case StoreHandler.SERVICE_IS_NOW_READY_RETRY_OPERATION:

                CheckInventory(selectedProductIndex);

                return;
            case StoreHandler.ERROR_WRONG_SETTINGS:

                break;
            case StoreHandler.ERROR_BAZAAR_NOT_INSTALLED:

                break;
            case StoreHandler.ERROR_SERVICE_NOT_INITIALIZED:

                break;
            case StoreHandler.ERROR_INTERNAL:

                break;
            case StoreHandler.ERROR_OPERATION_CANCELLED:

                break;
            case StoreHandler.ERROR_CONSUME_PURCHASE:

                break;
            case StoreHandler.ERROR_NOT_LOGGED_IN:

                break;
            case StoreHandler.ERROR_HAS_NOT_PRODUCT_IN_INVENTORY:

                break;
            case StoreHandler.ERROR_CONNECTING_VALIDATE_API:

                break;
            case StoreHandler.ERROR_PURCHASE_IS_REFUNDED:

                break;
            case StoreHandler.ERROR_NOT_SUPPORTED_IN_EDITOR:

                break;
            case StoreHandler.ERROR_WRONG_PRODUCT_INDEX:

                break;
            case StoreHandler.ERROR_WRONG_PRODUCT_ID:

                break;
        }
        
        txtResult.text = "ErrorCode: " + errorCode + ", " + message + "\n" + txtResult.text;
        loadingDialog.SetActive(false);
    }
    
    private int GetCoins()
    {
        return PlayerPrefs.GetInt(COIN_KEY, 0);
    }

    private void AddCoin(int value)
    {
        int coins = GetCoins();
        PlayerPrefs.SetInt(COIN_KEY, coins + value);
        StartCoroutine(AnimateCountText(txtCoins, coins, coins + value));
    }

    private void EnableDoubleCoin()
    {
        doubleCoin = true;
        PlayerPrefs.SetInt(DOUBLE_COIN_KEY, 1);
        btnDoubleCoin.interactable = false;
    }

    private IEnumerator AnimateCountText(Text text, int preValue, int nextValue)
    {
        bool increase = true;
        if (nextValue < preValue)
        {
            increase = false;
        }

        float value = nextValue - preValue;

        float t = (Mathf.Abs(value) / 5) * 0.4f;
        if (t > 2.0f) t = 2.0f;

        if (value != 0)
        {
            float step = value / (t / 0.06f);
            float pre = preValue;

            value = Mathf.Abs(value);

            while (value > 0)
            {
                value -= Mathf.Abs(step);
                pre += (step);
                if ((increase && pre > nextValue) || (!increase && pre < nextValue))
                {
                    pre = nextValue;
                }

                text.text = (int)pre + "";
                yield return new WaitForSecondsRealtime(0.02f);
            }

            text.text = nextValue + "";
        }
        else
        {
            text.text = nextValue + "";
        }
    }
}