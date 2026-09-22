using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HQIapSimulatedView : MonoBehaviour
{
    public Action<HQIapProduct> OnSimulatedPayment;
    public Action OnSimulatedCancel;
    private Text _overviewTxt;
    private Text _keyTxt;
    private Text _valueTxt;
    private HQIapProduct _product;
    public static HQIapSimulatedView ShowView()
    {
        GameObject prefab = Resources.Load<GameObject>("HQIapSimulatedView");
        HQIapSimulatedView iapSimulatedView = Instantiate(prefab).GetComponent<HQIapSimulatedView>();
        return iapSimulatedView;
    }

    public void LoadData(string id, string scene, bool initialized, Dictionary<string, HQIapProduct> products)
    {
        string initStr = initialized ? "初始化已完成" : "初始化未完成";
        string txt = "";
        txt += $"{initStr}\n";
        txt += $"当前购买的商品ID:{id}   购买场景:{scene} 商品总数:{products.Count}\n";

        if (products == null)
        {
            txt += "商品表没有获取到";
        }
        else if (string.IsNullOrEmpty(id))
        {
            txt += "商品id为空";
        }
        else if (!products.TryGetValue(id, out _product))
        {
            txt += $"商品表中不存在商品:{id}";
        }
        else
        {
            txt += $"当前商品信息为:";
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(_product);
            Dictionary<string, object> dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            string keyTxt = "";
            string valueTxt = "";

            //放在最后打印
            object payouts = null;
            dict.TryGetValue("Payouts", out payouts);
            dict.Remove("Payouts");

            foreach (var item in dict)
            {
                keyTxt += $"{item.Key}:\n";
                valueTxt += $"{(item.Value == null ? "null" : item.Value)}\n";
            }

            keyTxt += "Payouts";
            valueTxt += $"{(payouts == null ? "null" : payouts)}";

            _keyTxt.text = keyTxt;
            _valueTxt.text = valueTxt;
        }

        _overviewTxt.text = txt;
    }
    void Awake()
    {
        var PermanentCloseBtn = transform.Find("PermanentCloseBtn").GetComponent<Button>();
        var SingleCloseBtn = transform.Find("SingleCloseBtn").GetComponent<Button>();
        var PayBtn = transform.Find("PayBtn").GetComponent<Button>();
        var CancelBtn = transform.Find("CancelBtn").GetComponent<Button>();

        _overviewTxt = transform.Find("DescribeView/OverviewTxt").GetComponent<Text>();
        _keyTxt = transform.Find("DescribeView/Products/KeyTxt").GetComponent<Text>();
        _valueTxt = transform.Find("DescribeView/Products/ValueTxt").GetComponent<Text>();

        PermanentCloseBtn.onClick.AddListener(OnPermanentCloseBtnClick);
        SingleCloseBtn.onClick.AddListener(OnSingleCloseBtnClick);
        PayBtn.onClick.AddListener(OnPayBtnClick);
        CancelBtn.onClick.AddListener(OnCancelBtnClick);
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    #region OnClick
    void OnPermanentCloseBtnClick()
    {
        HQIap.DisableSimulatedPurchase();

    }

    void OnSingleCloseBtnClick()
    {
        HQIap.EnableSimulatedPurchase = false;
    }

    void OnPayBtnClick()
    {
        OnSimulatedPayment.Invoke(_product);
        CloseView();
    }

    void OnCancelBtnClick()
    {
        OnSimulatedCancel.Invoke();
        CloseView();
    }

    void CloseView()
    {
        OnSimulatedPayment = null;
        OnSimulatedCancel = null;
        Destroy(gameObject);
    }

    #endregion
}