using System;
using System.Globalization;
using System.Collections.Generic;
using Nexus.Events;
using Nexus.Services;
using Nexus.Persistence;
using Nexus.Core.Validation;

namespace Nexus.Settings
{
    public sealed class SettingsService : ISettingsService
    {
        private abstract class SettingEntry
        {
            public string Key;
            public Type ValueType;
            public string Category;
            public object CurrentValue;
            public object DefaultValue;

            public abstract bool Validate(object value);
            public abstract string ToStorageString(object value);
            public abstract object FromStorageString(string text);
        }

        private sealed class SettingEntry<TValue> : SettingEntry
        {
            public Func<TValue, bool> Validator;

            public override bool Validate(object value) => Validator == null || Validator((TValue)value);

            public override string ToStorageString(object value)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            public override object FromStorageString(string text)
            {
                Type type = typeof(TValue);
                if (type.IsEnum)
                {
                    return Enum.Parse(type, text);
                }

                if (type == typeof(bool))
                {
                    return bool.Parse(text);
                }

                if (type == typeof(int))
                {
                    return int.Parse(text, CultureInfo.InvariantCulture);
                }

                if (type == typeof(float))
                {
                    return float.Parse(text, CultureInfo.InvariantCulture);
                }

                if (type == typeof(string))
                {
                    return text;
                }

                throw new NotSupportedException(
                    $"Setting type '{type.Name}' is not supported. Supported types: bool, int, float, string, enum.");
            }
        }

        private const string SaveKey = "Nexus.Settings";
        private const int SaveVersion = 1;

        private readonly Dictionary<string, SettingEntry> _entries = new();
        private IPersistenceService _persistence;
        private IEventService _events;
        private bool _isDirty;

        private readonly string _saveKey;
        private readonly int _saveVersion;

        public SettingsService(string saveKey = SaveKey, int saveVersion = SaveVersion)
        {
            _saveKey = Guard.NotNullOrEmpty(saveKey, nameof(saveKey));
            _saveVersion = Guard.AtLeast(saveVersion, 1, nameof(saveVersion));
        }

        public void Initialize(IServiceRegistry registry)
        {
            _persistence = registry.Get<IPersistenceService>();
            _events = registry.Get<IEventService>();
            Load();
        }

        public void Shutdown()
        {
            if (_isDirty)
            {
                Save();
            }

            _entries.Clear();
        }

        public void Register<TValue>(SettingDefinition<TValue> definition)
        {
            Guard.NotNullOrEmpty(definition.Key, nameof(definition.Key));

            if (_entries.ContainsKey(definition.Key))
            {
                throw new InvalidOperationException($"Setting '{definition.Key}' is already registered.");
            }

            if (definition.Validator != null && !definition.Validator(definition.DefaultValue))
            {
                throw new ArgumentException(
                    $"Default value for setting '{definition.Key}' does not satisfy its own validator.");
            }

            _entries.Add(definition.Key, new SettingEntry<TValue>()
            {
                Key = definition.Key,
                ValueType = typeof(TValue),
                Category = definition.Category,
                DefaultValue = definition.DefaultValue,
                CurrentValue = definition.DefaultValue,
                Validator = definition.Validator
            });
        }

        public TValue Get<TValue>(string key)
        {
            SettingEntry entry = RequireEntry<TValue>(key);
            return (TValue)entry.CurrentValue;
        }

        public bool TryGet<TValue>(string key, out TValue value)
        {
            if (_entries.TryGetValue(key, out SettingEntry entry) && entry.ValueType == typeof(TValue))
            {
                value = (TValue)entry.CurrentValue;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<TValue>(string key, TValue value)
        {
            SettingEntry entry = RequireEntry<TValue>(key);

            if (!entry.Validate(value))
            {
                throw new ArgumentException($"Value '{value}' is not valid for setting '{key}'.");
            }

            ApplyValue(entry, value);
        }

        public void ResetToDefault(string key)
        {
            if (_entries.TryGetValue(key, out SettingEntry entry))
            {
                ApplyValue(entry, entry.DefaultValue);
            }
        }

        public void ResetAllToDefaults()
        {
            foreach (SettingEntry entry in _entries.Values)
            {
                ApplyValue(entry, entry.DefaultValue);
            }
        }

        public IEnumerable<string> GetKeysInCategory(string category)
        {
            foreach (SettingEntry entry in _entries.Values)
            {
                if (string.Equals(entry.Category, category, StringComparison.Ordinal))
                {
                    yield return entry.Key;
                }
            }
        }

        public void Save()
        {
            var snapshot = new SettingsSnapshot();
            foreach (SettingEntry entry in _entries.Values)
            {
                snapshot.Keys.Add(entry.Key);
                snapshot.Values.Add(entry.ToStorageString(entry.CurrentValue));
            }

            _persistence.Save(_saveKey, snapshot, _saveVersion);
            _isDirty = false;
        }

        public void Load()
        {
            SettingsSnapshot snapshot = _persistence.Load(_saveKey, _saveVersion, new SettingsSnapshot());

            for (int i = 0; i < snapshot.Keys.Count; i++)
            {
                string key = snapshot.Keys[i];
                if (!_entries.TryGetValue(key, out SettingEntry entry))
                {
                    continue; // setting no longer exists in this build - ignore
                }

                object parsed;
                try
                {
                    parsed = entry.FromStorageString(snapshot.Values[i]);
                }
                catch
                {
                    continue; // corrupted individual value - keep the current (default) value
                }

                if (!entry.Validate(parsed))
                {
                    continue; // invalid persisted value - keep the current (default) value
                }

                ApplyValue(entry, parsed);
            }

            _isDirty = false;
        }

        private SettingEntry RequireEntry<TValue>(string key)
        {
            if (!_entries.TryGetValue(key, out SettingEntry entry))
            {
                throw new InvalidOperationException($"Setting '{key}' is not registered.");
            }

            if (entry.ValueType != typeof(TValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{key}' is type '{entry.ValueType.Name}', not '{typeof(TValue).Name}'.");
            }

            return entry;
        }

        private void ApplyValue(SettingEntry entry, object value)
        {
            if (Equals(entry.CurrentValue, value))
            {
                return;
            }

            entry.CurrentValue = value;
            _isDirty = true;
            _events.Publish(new SettingChangedEvent(entry.Key));
        }
    }
}
