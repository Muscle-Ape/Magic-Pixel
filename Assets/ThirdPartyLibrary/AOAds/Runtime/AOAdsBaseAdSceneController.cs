using System.Collections.Generic;
using System.Linq;

public class AOAdsBaseAdSceneController
{

    private HashSet<string> _adSceneMap;
    public void SetActivePlaces(string[] adPlaces)
    {
        _adSceneMap = adPlaces == null ? null : new HashSet<string>(adPlaces);
    }

    public List<string> GetActiveScene()
    {
        if (_adSceneMap == null)
        {
            return new List<string>();
        }
        else
        {
            return _adSceneMap.ToList();
        }
    }
    public virtual bool IsEnabled(string adPlace)
    {
        //无位置信息,默认全部起效
        if (_adSceneMap == null || _adSceneMap.Count == 0)
        {
            return true;
        }

        //判断是否是活跃位置
        if (_adSceneMap.Contains(adPlace))
        {
            return true;
        }

        return false;
    }
}
