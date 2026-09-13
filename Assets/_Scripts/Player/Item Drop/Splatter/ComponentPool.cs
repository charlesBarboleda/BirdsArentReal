using System.Collections.Generic;
using UnityEngine;

namespace CoreUtilities
{
    /// <summary>
    /// Minimal generic object pool for any prefab carrying a Component of type T.
    /// Not tied to any specific feature or project - drop this into any Unity project.
    /// </summary>
    public class ComponentPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _inactive = new Stack<T>();

        public ComponentPool(T prefab, int prewarmCount = 0, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < prewarmCount; i++)
            {
                T instance = CreateNew();
                instance.gameObject.SetActive(false);
                _inactive.Push(instance);
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = _inactive.Count > 0 ? _inactive.Pop() : CreateNew();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public void Release(T instance)
        {
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_parent, false);
            _inactive.Push(instance);
        }

        private T CreateNew() => Object.Instantiate(_prefab, _parent);
    }
}