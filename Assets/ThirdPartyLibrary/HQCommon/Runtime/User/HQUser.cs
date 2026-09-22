using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Runtime.CompilerServices;


public class HQUser<T> where T : new()
{
    public delegate void PropertyChangedEventHandler(object value, string propertyName);
    public static event PropertyChangedEventHandler PropertyWillChanged;
    public static event PropertyChangedEventHandler PropertyDidChanged;

    public static string kPlayerPrefHQUser = "__kPlayerPrefHQUser__";
    public string AccountID;//账户id
    public string DistinctID;//访客id

    public int IapTotalAmount;//iap总金额
    public List<HQIapRecord> IapRecords = new List<HQIapRecord>();
    public static bool FirstCreateUser;
    private static T _instance;
    public static T Instance { set { _instance = value; } get { return _instance; } }

    public HQUser()
    {
        if (FirstCreateUser)
        {
            InitInstance();
        }
    }
    public static T Load()
    {
        string json = PlayerPrefs.GetString(kPlayerPrefHQUser, null);
        if (json == null)
        {
            FirstCreateUser = true;

            _instance = new T();
        }
        else
        {
            Debug.Log("[HQUser] Load:" + json);
            T obj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            if (obj == null)
            {
                FirstCreateUser = true;
                _instance = new T();
            }
            else
            {
                _instance = obj;
            }
        }

        return _instance;
    }

    protected virtual void InitInstance()
    {
        Debug.LogError("[HQUser] 未初始化实例!!!");
    }

    public static void Save()
    {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(_instance);
        Debug.Log("[HQUser] json:" + json);
        PlayerPrefs.SetString(kPlayerPrefHQUser, json);
        PlayerPrefs.Save();
    }

    public int GetProductBuyCount(string productId)
    {
        if (IapRecords == null || IapRecords.Count == 0) return 0;
        if (string.IsNullOrEmpty(productId)) return 0;

        int count = IapRecords.Where(r => r.ProductID == productId).ToArray().Length;
        return count;
    }

    protected void willChange(object value, [CallerMemberName] string propertyName = "")
    {
        if (PropertyWillChanged != null)
        {
            PropertyWillChanged(value, propertyName);
        }
    }

    protected void didChanged(object value, [CallerMemberName] string propertyName = "")
    {
        if (PropertyDidChanged != null)
        {
            PropertyDidChanged(value, propertyName);
        }
    }

    // void Log(object message)
    // {
    //     Debug.Log("[HQUser] " + message);
    // }


}

public class HQIapRecord
{
    public string ProductID;
    public long CreatedAt;
}

public static class Int32Extension
{
    public static bool Sub(this ref int num, int change)
    {
        int absChange = System.Math.Abs(change);
        if (num < absChange)
        {
            return false;
        }
        else
        {
            num = num - absChange;
            return true;
        }
    }

    public static void Change(this ref int num, int change)
    {

    }

}