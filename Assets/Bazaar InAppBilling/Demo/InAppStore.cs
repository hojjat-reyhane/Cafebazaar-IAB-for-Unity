using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InAppStore : MonoBehaviour
{
    public GameObject successDialog, errorDialog, loadingDialog;
    public Text txtResult;
    public Text txtCoins;
    public Button btnDoubleCoin;
    
    private bool doubleCoin = false;
    private readonly string COIN_KEY = "coin";
    private readonly string DOUBLE_COIN_KEY = "doubleCoin";

    void Start()
    {
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
        loadingDialog.SetActive(true);
        StoreHandler.instance.Purchase(index, OnPurchaseFailed, OnPurchasedSuccessfully);
    }

    public void CheckInventory(int index)
    {
        loadingDialog.SetActive(true);
        StoreHandler.instance.CheckInventory(index, OnInventoryCheckFailed, OnInventoryHadProduct);
    }

    public void SetValidatePurchasesState(bool state)
    {
        StoreHandler.instance.validatePurchases = state;
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
        CheckErrorCode(errorCode);

        txtResult.text = "ErrorCode: " + errorCode + ", " + message + "\n" + txtResult.text;
        errorDialog.SetActive(true);
        loadingDialog.SetActive(false);
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
        CheckErrorCode(errorCode);

        txtResult.text = "ErrorCode: " + errorCode + ", " + message + "\n" + txtResult.text;
        loadingDialog.SetActive(false);
    }
    
    private void CheckErrorCode(int errorCode)
    {
        switch (errorCode)
        {
            case StoreHandler.ERROR_WRONG_SETTINGS:
                // Enter public key (RSA from Bazaar panel) in StoreHandler.cs
                break;
            case StoreHandler.ERROR_BAZAAR_NOT_INSTALLED:
                // User didn't install CafeBazaar application.
                break;
            case StoreHandler.ERROR_SERVICE_NOT_INITIALIZED:
                // Billing service isn't initialized. Encorage user to retry.
                break;
            case StoreHandler.ERROR_INTERNAL:
                // Internal error happened. Follow the instructions carefully and if it persists contact Bazaar developer support.
                break;
            case StoreHandler.ERROR_OPERATION_CANCELLED:
                // Operation cancelled by user or failed.
                break;
            case StoreHandler.ERROR_CONSUME_PURCHASE:
                // Purchase was successful but couldn't consume it. So the purchase is stored in user's inventory and can be restore later without paying.
                break;
            case StoreHandler.ERROR_NOT_LOGGED_IN:
                // User is not logged in to Cafebazaar application so can't check the inventory.
                break;
            case StoreHandler.ERROR_HAS_NOT_PRODUCT_IN_INVENTORY:
                // User has not this product in the inventory or the product is consumed.
                break;
            case StoreHandler.ERROR_CONNECTING_VALIDATE_API:
                // Couldn't connect validating API due to internet connection failure or wrong client info in StoreHandler.cs.
                break;
            case StoreHandler.ERROR_PURCHASE_IS_REFUNDED:
                // Purchase is refunded
                break;
            case StoreHandler.ERROR_NOT_SUPPORTED_IN_EDITOR:
                // You can't use In App Billing in Editor mode. It only works on Android devices.
                break;
        }
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