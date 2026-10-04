using System;
using System.Collections.Generic;
using Nexus.Services;
using Nexus.Diagnostics;
using Nexus.Core.Validation;

namespace Nexus.Persistence
{
    public sealed class PersistenceService : IPersistenceService
    {
        private readonly IPersistenceStorage _storage;
        private readonly IPersistenceSerializer _serializer;
        private readonly Dictionary<string, Dictionary<int, ISaveMigration>> _migrations =
            new();

        private ILoggingService _log;

        public PersistenceService(IPersistenceStorage storage, IPersistenceSerializer serializer)
        {
            _storage = Guard.NotNull(storage, nameof(storage));
            _serializer = Guard.NotNull(serializer, nameof(serializer));
        }

        public void Initialize(IServiceRegistry registry)
        {
            registry.TryGet(out _log);
        }

        public void Shutdown()
        {
            _migrations.Clear();
        }

        public bool Exists(string key) => _storage.Exists(key);

        public void Delete(string key) => _storage.Delete(key);

        public void Save<TData>(string key, TData data, int version)
        {
            Guard.NotNullOrEmpty(key, nameof(key));

            var envelope = new SaveEnvelope
            {
                Version = version,
                Payload = _serializer.Serialize(data)
            };

            _storage.WriteText(key, _serializer.Serialize(envelope));
        }

        public TData Load<TData>(string key, int currentVersion, TData defaultValue)
        {
            Guard.NotNullOrEmpty(key, nameof(key));

            if (!_storage.Exists(key))
            {
                return defaultValue;
            }

            try
            {
                string rawText = _storage.ReadText(key);
                SaveEnvelope envelope = _serializer.Deserialize<SaveEnvelope>(rawText);

                while (envelope.Version < currentVersion)
                {
                    if (!TryGetMigration(key, envelope.Version, out ISaveMigration migration))
                    {
                        _log?.Log(
                            LogLevel.Error,
                            "Persistence",
                            $"No migration registered for '{key}' from version {envelope.Version} to {currentVersion}.");
                        return defaultValue;
                    }

                    envelope.Payload = migration.Migrate(envelope.Payload);
                    envelope.Version = migration.ToVersion;
                }

                return _serializer.Deserialize<TData>(envelope.Payload);
            }
            catch (Exception exception)
            {
                _log?.LogException(exception, "Persistence");
                BackupCorruptData(key);
                return defaultValue;
            }
        }

        public void RegisterMigration(string key, ISaveMigration migration)
        {
            Guard.NotNullOrEmpty(key, nameof(key));
            Guard.NotNull(migration, nameof(migration));

            if (!_migrations.TryGetValue(key, out Dictionary<int, ISaveMigration> stepsForKey))
            {
                stepsForKey = new Dictionary<int, ISaveMigration>();
                _migrations.Add(key, stepsForKey);
            }

            if (stepsForKey.ContainsKey(migration.FromVersion))
            {
                throw new InvalidOperationException(
                    $"A migration from version {migration.FromVersion} is already registered for '{key}'.");
            }

            stepsForKey.Add(migration.FromVersion, migration);
        }

        private bool TryGetMigration(string key, int fromVersion, out ISaveMigration migration)
        {
            if (_migrations.TryGetValue(key, out Dictionary<int, ISaveMigration> stepsForKey))
            {
                return stepsForKey.TryGetValue(fromVersion, out migration);
            }

            migration = null;
            return false;
        }

        private void BackupCorruptData(string key)
        {
            try
            {
                string rawText = _storage.ReadText(key);
                if (rawText != null)
                {
                    _storage.WriteText(key + ".corrupt", rawText);
                }
            }
            catch (Exception exception)
            {
                _log?.LogException(exception, "Persistence");
            }
        }
    }
}
