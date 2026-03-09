using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EFT.Animations;
using HarmonyLib;
using UnityEngine;

namespace BetterVision
{
    public static class SuperGet
    {
        private static Dictionary<Tuple<Type, string>, Func<object, object>> cacheTypeProp =
            new Dictionary<Tuple<Type, string>, Func<object, object>>();

        private static Dictionary<Tuple<Type, string>, Action<object, object>> cacheTypeSetProp =
            new Dictionary<Tuple<Type, string>, Action<object, object>>();

        private static Dictionary<Tuple<Type, Type>, Func<object, object>> cacheTypeType =
            new Dictionary<Tuple<Type, Type>, Func<object, object>>();

        public static object GetValue(object obj, params string[] vs)
        {
            object obj2 = obj;
            foreach (string text in vs)
            {
                obj2 = SuperGet.Get(obj2, text);
                if (obj2 == null)
                {
                    return obj2;
                }
            }
            return obj2;
        }

        private static object Get(object obj, string name)
        {
            Type type = obj.GetType();
            Func<object, object> func;
            if (!SuperGet.cacheTypeProp.TryGetValue(new Tuple<Type, string>(type, name), out func))
            {
                FieldInfo fi = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                {
                    if (fi.IsStatic)
                    {
                        func = (_) => fi.GetValue(type);
                    }
                    else
                    {
                        func = fi.GetValue;
                    }
                }

                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    func = property.GetValue;
                }

                SuperGet.cacheTypeProp.Add(new Tuple<Type, string>(type, name), func);
            }

            if (func != null)
            {
                return func(obj);
            }
            return null;
        }

        public static void SetValue(object obj, object value, params string[] vs)
        {
            object obj2 = obj;
            int num = 0;
            for (int i = 0; i < vs.Length - 1; i++)
            {
                obj2 = SuperGet.Get(obj2, vs[num]);
                if (obj2 == null)
                {
                    return;
                }
            }
            SuperGet.Set(obj2, value, vs[vs.Length - 1]);
        }

        private static void Set(object obj, object value, string name)
        {
            Type type = obj.GetType();
            Action<object, object> action;
            if (!SuperGet.cacheTypeSetProp.TryGetValue(new Tuple<Type, string>(type, name), out action))
            {
                FieldInfo fi = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                {
                    if (fi.IsStatic)
                    {
                        action = (_, v) => fi.SetValue(type, v);
                    }
                    else
                    {
                        action = fi.SetValue;
                    }
                }

                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    action = property.SetValue;
                }

                SuperGet.cacheTypeSetProp.Add(new Tuple<Type, string>(type, name), action);
            }

            if (action != null)
            {
                action(obj, value);
            }
        }

        public static object GetValueByType(object obj, Type type, bool allow = false)
        {
            Type otype = obj.GetType();
            Func<object, object> func = (_) => null;

            if (!SuperGet.cacheTypeType.TryGetValue(new Tuple<Type, Type>(otype, type), out func))
            {
                FieldInfo[] fields = otype.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                int i = 0;
                while (i < fields.Length)
                {
                    FieldInfo field = fields[i];
                    if (field.FieldType == type || (allow && type.IsAssignableFrom(field.FieldType)))
                    {
                        if (field.IsStatic)
                        {
                            func = (_) => field.GetValue(otype);
                            goto END;
                        }
                        func = (o) => field.GetValue(o);
                        goto END;
                    }
                    else
                    {
                        i++;
                    }
                }

                PropertyInfo[] properties = otype.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                i = 0;
                while (i < properties.Length)
                {
                    PropertyInfo prop = properties[i];
                    if (prop.PropertyType == type || (allow && type.IsAssignableFrom(prop.PropertyType)))
                    {
                        if (prop.GetMethod != null && prop.GetMethod.IsStatic)
                        {
                            func = (_) => prop.GetValue(otype);
                            break;
                        }
                        func = (o) => prop.GetValue(o);
                        break;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

        END:
            return func(obj);
        }

        public static T GetValue<T>(object obj, bool allow = false) where T : class
        {
            Type typeFromHandle = typeof(T);
            return SuperGet.GetValueByType(obj, typeFromHandle, allow) as T;
        }
    }

    [HarmonyPatch(typeof(ProceduralWeaponAnimation), "InitTransforms")]
    internal class T7ScopeHook
    {
        [HarmonyPostfix]
        public static void H(ProceduralWeaponAnimation __instance)
        {
            if (BetterVision.T7BlockScope.Value)
                return;

            Process(__instance);
        }

        public static void Process(Transform transform)
        {
            Component[] componentsInChildren = transform.GetComponentsInChildren(typeof(MeshRenderer), true);
            for (int i = 0; i < componentsInChildren.Length; i++)
            {
                MeshRenderer mr = componentsInChildren[i] as MeshRenderer;
                if (mr == null)
                    continue;

                Material[] materials = mr.materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    if (materials[j] != null)
                    {
                        materials[j].SetFloat("_ThermalVisionOn", 0f);
                    }
                }
            }
        }

        public static void Process(ProceduralWeaponAnimation weaponAnimation)
        {
            object scopeListObj = SuperGet.GetValue(weaponAnimation, "ScopeAimTransforms");
            if (scopeListObj == null)
                return;

            IList list = scopeListObj as IList;
            if (list == null)
                return;

            foreach (object obj in list)
            {
                object boneObj = SuperGet.GetValue(obj, "Bone");
                Transform bone = boneObj as Transform;
                if (bone == null)
                    continue;

                Transform root = bone.parent != null ? bone.parent : bone;
                Process(root);
            }
        }
    }
}
