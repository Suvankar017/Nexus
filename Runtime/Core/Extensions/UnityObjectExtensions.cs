using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Lifecycle and null-safety helpers for <see cref="UnityEngine.Object"/> references.
    /// <para>
    /// <b>Why this is necessary:</b><br/>
    /// A destroyed Unity object is not garbage-collected immediately; its C# wrapper remains alive 
    /// while the underlying C++ native object is deallocated. Unity overrides <c>operator ==</c> 
    /// to make destroyed objects compare equal to <c>null</c>.
    /// </para>
    /// <para>
    /// However, this overload is silently bypassed when:
    /// <list type="bullet">
    /// <item>Using the C# null-conditional operator (<c>obj?.Method()</c>)</item>
    /// <item>Using the null-coalescing operator (<c>obj ?? fallback</c>)</item>
    /// <item>Accessing objects through generic parameters (<c>T</c>)</item>
    /// <item>Accessing objects through interfaces (e.g., <c>IDamageable</c>)</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class UnityObjectExtensions
    {
        // =========================================================================
        // 1. CORE NULL & LIFECYCLE CHECKS
        // =========================================================================

        /// <summary>
        /// Returns <c>true</c> if the reference is a pure C# null OR has been destroyed by Unity.
        /// </summary>
        public static bool IsNullOrDestroyed(this Object unityObject)
        {
            return unityObject == null;
        }

        /// <summary>
        /// Returns <c>true</c> if the reference is valid, non-null, and not destroyed.
        /// </summary>
        public static bool IsAlive(this Object unityObject)
        {
            return unityObject != null;
        }

        /// <summary>
        /// Polymorphic check that safely checks if an arbitrary object or interface
        /// is either a C# null OR a destroyed <see cref="Object"/>.
        /// <para>
        /// Critical for interface references like <c>IDamageable</c> or <c>IInteractable</c>
        /// implemented by <see cref="MonoBehaviour"/> classes.
        /// </para>
        /// </summary>
        public static bool IsNullOrDestroyed(this object obj)
        {
            if (obj is null) return true;
            if (obj is Object unityObj) return unityObj == null;
            return false;
        }

        /// <summary>
        /// Polymorphic check that returns <c>true</c> only if the object/interface is not null 
        /// and (if it is a Unity object) has not been destroyed.
        /// </summary>
        public static bool IsAlive(this object obj)
        {
            return !obj.IsNullOrDestroyed();
        }


        // =========================================================================
        // 2. SAFE OPERATORS (Fixes `?.` and `??`)
        // =========================================================================

        /// <summary>
        /// Converts a fake "Unity null" (destroyed object) into a true C# <c>null</c>.
        /// <para>
        /// <b>Fixes the broken C# <c>?.</c> operator:</b><br/>
        /// Standard: <c>myComponent?.DoSomething()</c> &#x2192; <i>Throws MissingReferenceException if destroyed!</i><br/>
        /// Fixed: <c>myComponent.AsAlive()?.DoSomething()</c> &#x2192; <i>Safely evaluates to null.</i>
        /// </para>
        /// <para>
        /// <b>Fixes the broken C# <c>??</c> operator:</b><br/>
        /// <c>var target = currentTarget.AsAlive() ?? fallbackTarget;</c>
        /// </para>
        /// </summary>
        public static T AsAlive<T>(this T unityObject) where T : Object
        {
            return unityObject == null ? null : unityObject;
        }


        // =========================================================================
        // 3. SAFE DESTRUCTION & CLEANUP
        // =========================================================================

        /// <summary>
        /// Destroys the object safely across both Play Mode (<see cref="Object.Destroy(Object)"/>) 
        /// and Edit Mode (<see cref="Object.DestroyImmediate(Object)"/>).
        /// </summary>
        public static void SafeDestroy(this Object unityObject)
        {
            if (unityObject == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(unityObject);
                return;
            }
#endif
            Object.Destroy(unityObject);
        }

        /// <summary>
        /// Safely destroys the underlying <see cref="GameObject"/> of a Component.
        /// </summary>
        public static void SafeDestroyGameObject(this Component component)
        {
            if (component == null) return;
            component.gameObject.SafeDestroy();
        }


        // =========================================================================
        // 4. SAFE LOGGING / DIAGNOSTICS
        // =========================================================================

        /// <summary>
        /// Returns the name of the Unity Object without throwing a <see cref="MissingReferenceException"/>
        /// if the object has already been destroyed.
        /// </summary>
        public static string SafeName(this Object unityObject, string fallback = "<null or destroyed>")
        {
            if (unityObject == null) return fallback;

            try
            {
                return unityObject.name;
            }
            catch (MissingReferenceException)
            {
                return fallback;
            }
        }


        // =========================================================================
        // 5. LINQ & COLLECTION HELPERS
        // =========================================================================

        /// <summary>
        /// Filters an enumerable to exclude both C# nulls and destroyed Unity objects.
        /// </summary>
        public static IEnumerable<T> WhereAlive<T>(this IEnumerable<T> source) where T : Object
        {
            if (source == null) yield break;

            foreach (var item in source)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// In-place removal of all destroyed Unity objects from a List.
        /// Avoids GC allocations caused by LINQ <c>Where()</c> chains.
        /// </summary>
        public static int RemoveDestroyed<T>(this List<T> list) where T : Object
        {
            if (list == null) return 0;
            return list.RemoveAll(item => item == null);
        }
    }
}
