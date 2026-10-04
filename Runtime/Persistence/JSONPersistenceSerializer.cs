using UnityEngine;

namespace Nexus.Persistence
{
    /// <summary>
    /// <see cref="JsonUtility"/>-backed serializer — the only serializer Phase 2 provides (a
    /// second one, e.g. binary, isn't built speculatively; add it if a concrete need shows up).
    /// Inherits JsonUtility's constraints: <typeparamref name="TData"/> must be a
    /// <c>[Serializable]</c> type with public/serialized fields — properties, interfaces, and
    /// polymorphic fields aren't supported. This is exactly the shape of the small, dedicated
    /// data classes (e.g. <c>PlayerProgressData</c>) this framework already asks save data to be.
    /// </summary>
    public sealed class JSONPersistenceSerializer : IPersistenceSerializer
    {
        private readonly bool _prettyPrint;

        public JSONPersistenceSerializer(bool prettyPrint = true)
        {
            _prettyPrint = prettyPrint;
        }

        public string Serialize<TData>(TData data)
        {
            return JsonUtility.ToJson(data, _prettyPrint);
        }

        public TData Deserialize<TData>(string serialized)
        {
            return JsonUtility.FromJson<TData>(serialized);
        }
    }
}
