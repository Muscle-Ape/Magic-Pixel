using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class AOAdsBreakView : MonoBehaviour
{
    private static GameObject _breakView;
    public static void ShowView()
    {
        if (_breakView == null)
        {
            var prefab = Resources.Load<GameObject>("AOAdsBreakView");
            if (prefab == null)
            {
                Debug.LogError("[AOAds] AOAdsBreakView prefab not found in Resources");
                return;
            }
            _breakView = Instantiate(prefab);
        }
        _breakView.SetActive(true);        
    }

    public static void CloseView()
    {
        if (_breakView != null)
        {
            _breakView.SetActive(false);
        }
    }

}
