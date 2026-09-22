using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HQ.Cooldown
{
    public class Cooldown : MonoBehaviour
    {
        private string kPauseDateTime = "__kPauseDateTime__";
        private string kCooldownTimers = "__kCooldownTimers__";

        //CooldownAction 倒计时回调  status:回调类型 remainingTime:当前循环剩余时间 endCount:结束次数
        public delegate void CooldownAction(CooldownStatus status, float remainingTime, int endCount);
        private Dictionary<string, CooldownAction> _actions = new Dictionary<string, CooldownAction>();
        private Dictionary<string, CooldownTimer> _timers = new Dictionary<string, CooldownTimer>();
        private List<string> _willRemoveTimerkeys = new List<string>();
        private UInt32 _tickTockCount = 0;
        private int _targetFrameRate = 0;
        public Action _onInitialized;

#if UNITY_EDITOR
        private bool _inBackground = false;
#endif
        public static void Launch(Action onInitialized = null)
        {
            Instance.launch(onInitialized);
        }

        //设置倒计时
        public static void SetCooldown(CooldownTimer timer, CooldownAction action = null)
        {
            Instance.setCooldown(timer, action);
        }

        //设置倒计时 key:唯一标识 duration:每次持续时间 loopsCount:循环次数 action:回调
        //duration * loopsCount = 总时间
        public static void SetCooldown(string key, float duration, int loopsCount = 1, CooldownAction action = null)
        {
            Instance.setCooldown(key, duration, loopsCount, action);
        }

        public static CooldownTimer GetCooldown(string key)
        {
            return Instance.getCooldown(key);
        }

        // //增肌倒计时循环次数
        // public static void AddCooldownLoop(string key, int loopsCount)
        // {
        //     Instance.addCooldownLoop(key, loopsCount);
        // }

        //判断倒计时是否存在
        public static bool HasCooldown(string key)
        {
            return Instance.hasCooldown(key);
        }

        //设置倒计时回调
        public static void SetAction(string key, CooldownAction action)
        {
            Instance.setAction(key, action);
        }

        //移除倒计时
        public static void RemoveCooldown(CooldownTimer timer)
        {
            Instance.removeCooldown(timer);
        }

        //移除倒计时
        public static void RemoveCooldown(string key)
        {
            Instance.removeCooldown(key);
        }

        void loadData()
        {
            string json = PlayerPrefs.GetString(kCooldownTimers, null);
            Log("Get Json:" + json);
            if (!string.IsNullOrEmpty(json))
            {
                CooldownTimer[] timers = Newtonsoft.Json.JsonConvert.DeserializeObject<CooldownTimer[]>(json);
                foreach (var t in timers)
                {
                    _timers[t.Key] = t;
                }
            }
        }
        private void launch(Action onInitialized = null)
        {
            loadData();
            enterForeground();
            if (onInitialized != null) onInitialized();
        }

        private void setAction(string key, CooldownAction action)
        {
            if (!string.IsNullOrEmpty(key) && action != null)
            {
                _actions[key] = action;
            }

        }
        //循环次数 * 持续时间 = 总时间
        private void setCooldown(string key, float duration, int loopsCount = 1, CooldownAction action = null)
        {
            CooldownTimer timer = new CooldownTimer();
            timer.Key = key;
            timer.Duration = duration;
            timer.RemainingTime = duration;
            timer.LoopsCount = loopsCount - 1;//扣除一次给到剩余时间
            setCooldown(timer, aciotn: action);
        }

        private void setCooldown(CooldownTimer timer, CooldownAction aciotn = null)
        {
            _timers[timer.Key] = timer;
            if (aciotn != null)
            {
                _actions[timer.Key] = aciotn;
            }
        }

        private void addCooldownLoop(string key, int loopsCount)
        {
            CooldownTimer timer = getCooldown(key);
            if (timer != null)
            {
                timer.LoopsCount += loopsCount;
            }
        }
        private CooldownTimer getCooldown(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            CooldownTimer timer = null;
            _timers.TryGetValue(key, out timer);
            return timer;
        }
        private bool hasCooldown(string key)
        {
            return _timers.ContainsKey(key);
        }

        private void removeCooldown(CooldownTimer timer)
        {
            if (timer == null) return;
            removeCooldown(timer.Key);
        }

        private void removeCooldown(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _willRemoveTimerkeys.Add(key);
        }



        void Awake()
        {
#if UNITY_EDITOR
            Application.runInBackground = false;
#endif
            Log("Awake");
        }
        void Start()
        {
            _targetFrameRate = 25;
        }

        // Update is called once per frame
        void Update()
        {
#if UNITY_EDITOR
            if (_inBackground) return;
#endif
            _tickTockCount++;

            foreach (var timer in _timers.Values)
            {
                timer.RemainingTime -= Time.deltaTime;
                if (timer.Mode == TickTockMode.Second)
                {
                    if (_tickTockCount % _targetFrameRate == 0)
                    {
                        TickTockHandler(timer);
                    }
                }
                else
                {
                    TickTockHandler(timer);
                }
            }

            foreach (var key in _willRemoveTimerkeys)
            {
                _timers.Remove(key);
                _actions.Remove(key);
            }

            _willRemoveTimerkeys.Clear();
        }

        void TickTockHandler(CooldownTimer timer)
        {
            CooldownAction action = null;
            if (_actions.TryGetValue(timer.Key, out action))
            {
                if (timer.RemainingTime < 0)
                {
                    timer.RemainingTime = Math.Max(timer.RemainingTime, 0);
                    action(CooldownStatus.LoopEnd, timer.RemainingTime, 1);

                    if (timer.LoopsCount > 0)
                    {
                        timer.RemainingTime = timer.Duration;
                        timer.LoopsCount--;
                    }
                    else if (timer.LoopsCount == 0)
                    {
                        _willRemoveTimerkeys.Add(timer.Key);
                    }
                }
                else
                {
                    action(CooldownStatus.TickTock, timer.RemainingTime, 0);
                }
            }
        }


        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                //后台
#if UNITY_EDITOR
                _inBackground = true;
#endif
                enterBackground();
            }
            else
            {
#if UNITY_EDITOR
                _inBackground = false;
#endif
                //前台
                enterForeground();
            }
        }

#if UNITY_EDITOR
        void OnApplicationQuit()
        {
            enterBackground();
        }
#endif
        private void enterBackground()
        {
            string datetime = DateTime.Now.ToString();
            Log("Enter Background Now:" + datetime);
            PlayerPrefs.SetString(kPauseDateTime, datetime);


            CooldownTimer[] timers = new CooldownTimer[_timers.Count];
            _timers.Values.CopyTo(timers, 0);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(timers);

            Log("Set Json:" + json);
            PlayerPrefs.SetString(kCooldownTimers, json);

            PlayerPrefs.Save();
        }

        private void enterForeground()
        {
            string dtStr = PlayerPrefs.GetString(kPauseDateTime);
            if (!string.IsNullOrEmpty(dtStr))
            {
                DateTime dt1 = DateTime.Now;
                Log("Enter Foreground Now:" + dt1.ToString() + " Save:" + dtStr);
                DateTime dt2 = DateTime.Parse(dtStr);
                TimeSpan span = dt1 - dt2;
                float spanTime = (float)span.TotalSeconds;
                foreach (CooldownTimer timer in _timers.Values)
                {
                    float remainingTime = timer.LoopsCount * timer.Duration + timer.RemainingTime;
                    float rtime = remainingTime - spanTime;
                    if (rtime > 0)
                    {
                        int remainingCount = (int)Math.Floor(rtime / timer.Duration);
                        int endCount = timer.LoopsCount - remainingCount;
                        timer.LoopsCount = remainingCount;
                        timer.RemainingTime = rtime - remainingCount * timer.Duration;

                        CooldownAction action = null;
                        if (_actions.TryGetValue(timer.Key, out action))
                        {
                            action(CooldownStatus.LoopEnd, timer.RemainingTime, endCount);
                        }
                    }
                    else
                    {
                        CooldownAction action = null;
                        if (_actions.TryGetValue(timer.Key, out action))
                        {
                            action(CooldownStatus.LoopEnd, 0, timer.LoopsCount + 1);
                        }
                    }
                }
                PlayerPrefs.DeleteKey(kPauseDateTime);
                PlayerPrefs.Save();
            }
        }
        private void Log(object message)
        {
            Debug.Log("[HQCooldown] " + message);
        }

        protected static Cooldown instance;
        private static bool _onApplicationQuit;

        public static Cooldown Instance
        {
            get
            {
                //避免Editor模式，OnDestroy中使用Mono单例报错
#if UNITY_EDITOR
                if (_onApplicationQuit)
                {
                    return new Cooldown();
                }
#endif


                if (instance == null)
                {
                    instance = FindObjectOfType<Cooldown>();
                    if (FindObjectsOfType<Cooldown>().Length > 1)
                    {
                        return instance;
                    }

                    if (instance == null)
                    {
                        string instanceName = typeof(Cooldown).Name;
                        GameObject instanceGO = GameObject.Find(instanceName);

                        if (instanceGO == null)
                        {
                            instanceGO = new GameObject(instanceName);
                        }

                        instance = instanceGO.AddComponent<Cooldown>();
                        DontDestroyOnLoad(instanceGO); //保证实例不会被释放                 
                    }
                }

                return instance;
            }
        }
    }

    public class CooldownTimer
    {
        public string Key;
        public float Duration;
        public float RemainingTime;
        public int LoopsCount = 0;
        public TickTockMode Mode;
        //后期增加自然时间,游戏时间区分
    }

    public enum TickTockMode
    {
        Second,
        Frame
    }
    public enum CooldownStatus
    {
        None,
        TickTock,
        LoopEnd
    }
}
