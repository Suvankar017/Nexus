using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Ergonomic and allocation-friendly extensions for <see cref="Component"/> and <see cref="GameObject"/>.
    /// </summary>
    public static class ComponentExtensions
    {
        // =========================================================================
        // 1. GET OR ADD COMPONENT
        // =========================================================================

        /// <summary>
        /// Returns the existing component of type <typeparamref name="T"/> on this GameObject, 
        /// or attaches and returns a new one if it does not exist.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out var component))
            {
                return component;
            }

            return gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Returns the existing component of type <typeparamref name="T"/> on this Component's GameObject, 
        /// or attaches and returns a new one if it does not exist.
        /// </summary>
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// Non-generic version of <see cref="GetOrAddComponent{T}(GameObject)"/>.
        /// </summary>
        public static Component GetOrAddComponent(this GameObject gameObject, Type type)
        {
            if (gameObject.TryGetComponent(type, out var component))
            {
                return component;
            }

            return gameObject.AddComponent(type);
        }

        /// <summary>
        /// Non-generic version of <see cref="GetOrAddComponent{T}(Component)"/>.
        /// </summary>
        public static Component GetOrAddComponent(this Component component, Type type)
        {
            return component.gameObject.GetOrAddComponent(type);
        }

        // =========================================================================
        // 2. EXISTENCE CHECKS (Zero Allocation)
        // =========================================================================

        /// <summary>
        /// Checks if a component of type <typeparamref name="T"/> exists on this GameObject without throwing or allocating.
        /// </summary>
        public static bool HasComponent<T>(this GameObject gameObject)
        {
            return gameObject.TryGetComponent<T>(out _);
        }

        /// <summary>
        /// Checks if a component of type <typeparamref name="T"/> exists on this Component's GameObject without allocating.
        /// </summary>
        public static bool HasComponent<T>(this Component component)
        {
            return component.TryGetComponent<T>(out _);
        }

        // =========================================================================
        // 3. STRICT HIERARCHY SEARCHES (Excluding Self)
        // =========================================================================

        /// <summary>
        /// Finds a component of type <typeparamref name="T"/> in the parent chain, 
        /// <c>EXCLUDING</c> the calling object's own GameObject.
        /// <para>
        /// (Standard Unity <see cref="Component.GetComponentInParent{T}()"/> checks self first, 
        /// which often returns unwanted local components).
        /// </para>
        /// </summary>
        public static T GetComponentInParentOnly<T>(this Component component, bool includeInactive = false) where T : class
        {
            Transform parent = component.transform.parent;
            return parent == null ? null : parent.GetComponentInParent<T>(includeInactive);
        }

        /// <summary>
        /// Finds all components of type <typeparamref name="T"/> in children, 
        /// <c>EXCLUDING</c> the calling object's own GameObject.
        /// </summary>
        public static void GetComponentsInChildrenOnly<T>(
            this Component component,
            List<T> results,
            bool includeInactive = false) where T : class
        {
            results.Clear();
            Transform transform = component.transform;
            int childCount = transform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (includeInactive || child.gameObject.activeSelf)
                {
                    child.GetComponentsInChildren(includeInactive, results);
                }
            }
        }

        // =========================================================================
        // 4. TRY-GET VARIATIONS
        // =========================================================================

        /// <summary>
        /// Attempts to find a component of type <typeparamref name="T"/> in parents.
        /// </summary>
        public static bool TryGetComponentInParent<T>(this Component component, out T result, bool includeInactive = false)
        {
            result = component.GetComponentInParent<T>(includeInactive);
            return result != null;
        }

        /// <summary>
        /// Attempts to find a component of type <typeparamref name="T"/> in children.
        /// </summary>
        public static bool TryGetComponentInChildren<T>(this Component component, out T result, bool includeInactive = false)
        {
            result = component.GetComponentInChildren<T>(includeInactive);
            return result != null;
        }

        // =========================================================================
        // 5. LAYERS & PHYSICS
        // =========================================================================

        /// <summary>
        /// Checks if the GameObject's layer is included inside a <see cref="LayerMask"/>.
        /// </summary>
        public static bool IsInLayerMask(this GameObject gameObject, LayerMask layerMask)
        {
            return (layerMask.value & (1 << gameObject.layer)) != 0;
        }

        /// <summary>
        /// Checks if the Component's GameObject layer is included inside a <see cref="LayerMask"/>.
        /// </summary>
        public static bool IsInLayerMask(this Component component, LayerMask layerMask)
        {
            return component.gameObject.IsInLayerMask(layerMask);
        }

        /// <summary>
        /// Recursively changes the layer of this GameObject and all of its child transforms.
        /// </summary>
        public static void SetLayerRecursively(this GameObject root, int layer)
        {
            root.layer = layer;
            var transform = root.transform;
            int childCount = transform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                transform.GetChild(i).gameObject.SetLayerRecursively(layer);
            }
        }

        /// <summary>
        /// Recursively changes the layer of this Component's root and all of its child transforms.
        /// </summary>
        public static void SetLayerRecursively(this Component component, int layer)
        {
            component.gameObject.SetLayerRecursively(layer);
        }

        // =========================================================================
        // 6. HIERARCHY MANIPULATION & DIAGNOSTICS
        // =========================================================================

        /// <summary>
        /// Returns the full hierarchy path of the GameObject (e.g., "UI/Screens/MainMenu/StartButton").
        /// Invaluable for debugging and error logging.
        /// </summary>
        public static string GetHierarchyPath(this GameObject gameObject)
        {
            Transform current = gameObject.transform;
            if (current.parent == null) return current.name;

            var builder = new System.Text.StringBuilder(current.name);
            while (current.parent != null)
            {
                current = current.parent;
                builder.Insert(0, "/").Insert(0, current.name);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the full hierarchy path of the Component's GameObject.
        /// </summary>
        public static string GetHierarchyPath(this Component component)
        {
            return component.gameObject.GetHierarchyPath();
        }

        /// <summary>
        /// Destroys all immediate child GameObjects of this transform.
        /// Works safely across both Play Mode and Edit Mode.
        /// </summary>
        public static void DestroyChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                Object.Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// Destroys all immediate child GameObjects of this Component's transform.
        /// </summary>
        public static void DestroyChildren(this Component component)
        {
            component.transform.DestroyChildren();
        }
    }
}
