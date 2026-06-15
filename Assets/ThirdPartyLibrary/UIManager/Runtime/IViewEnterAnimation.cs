using System;
using UnityEngine;
using System.Collections;

namespace HQ.UIManager
{
    public interface IViewEnterAnimation
    {
        IEnumerator OnPlayEnterAnimation();
    }
}