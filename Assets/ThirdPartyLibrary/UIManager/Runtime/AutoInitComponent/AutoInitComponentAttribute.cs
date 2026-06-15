using System;
using System.Reflection;
using UnityEngine;

namespace HQ.UIManager
{
    /// <summary>
    /// 自动初始化Component属性基类
    /// </summary>
    public class AutoInitComponentAttribute : Attribute
    {
        virtual public void AutoSetField(AComponent com, FieldInfo fieldInfo)
        {
            
        }

        virtual public void AutoSetProperty(AComponent com, PropertyInfo propertyInfo)
        {
            
        }

        virtual public void AutoSetMethod(AComponent com, MethodInfo methodInfo)
        {
            
        }
    }
}