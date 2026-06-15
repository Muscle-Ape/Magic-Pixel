using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HQ.UIManager
{
    public class UIAnimManager : MonoBehaviour
    {
        Dictionary<AWindow, bool> displayWindowList = new Dictionary<AWindow, bool>();
        public void DisplayWindow(AWindow window, bool display)
        {
            displayWindowList[window] = display;
        }

        public void DoViewEnterAnimation(AWindow window, AWindow lastWindow, bool display)
        {
            DisplayWindow(lastWindow, display);
            StartCoroutine(DoAnimation(window, lastWindow, display));
        }

        IEnumerator DoAnimation(AWindow window, AWindow lastWindow, bool display)
        {
            var view  = window as IViewEnterAnimation;
            if (view != null)
            {
                yield return view.OnPlayEnterAnimation();
                if (!lastWindow.IsDestoried)
                {
                    lastWindow.gameObject.SetActive(displayWindowList[lastWindow]);
                }
            }
            else
            {
                if (!lastWindow.IsDestoried)
                {
                    lastWindow.gameObject.SetActive(displayWindowList[lastWindow]);
                }
            }
        }
    }
}