using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace RealisticStars
{
    internal static class Extensions
    {
        public static T GetPrivateField<T>(this object obj, string field)
        {
            return (T)Traverse.Create(obj).Field(field).GetValue();
        }

        public static void SetPrivateField(this object obj, string field, object value)
        {
            Traverse.Create(obj).Field(field).SetValue(value);
        }

        public static void SetPrivateField<T>(string field, object value)
        {
            Traverse.Create(typeof(T)).Field(field).SetValue(value);
        }

        public static object InvokePrivateMethod(this object obj, string method, params object[] parameters)
        {
            return AccessTools.Method(obj.GetType(), method).Invoke(obj, parameters);
        }

        public static T InvokePrivateMethod<T>(this object obj, string method, params object[] parameters)
        {
            return (T)obj.InvokePrivateMethod(method, parameters);
        }

        public static object InvokePrivateMethod<T>(string method, params object[] parameters)
        {
            return AccessTools.Method(typeof(T), method).Invoke(null, parameters);
        }

        public static T InvokePrivateMethod<T, E>(string method, params object[] parameters)
        {
            return (T)InvokePrivateMethod<E>(method, parameters);
        }
    }
}

