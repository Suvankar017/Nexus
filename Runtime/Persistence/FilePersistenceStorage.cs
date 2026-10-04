using System.IO;
using UnityEngine;
using Nexus.Core.Validation;

namespace Nexus.Persistence
{
    /// <summary>
    /// Default production storage: one file per key under a "Saves" subfolder of
    /// <see cref="Application.persistentDataPath"/> (the platform-appropriate writable location on
    /// both Android and iOS — never a hard-coded path). Writes go to a temp file first, then are
    /// renamed into place, so a crash mid-write leaves either the previous file intact or the new
    /// one, never a half-written one. The 3-argument <c>File.Move(src, dst, overwrite)</c> isn't
    /// available in this project's actual API surface despite its .NET Standard 2.1 compatibility
    /// level setting, so an existing target is deleted immediately before the (2-argument) rename
    /// rather than replaced atomically by Move itself — a brief window exists between that delete
    /// and the rename where neither the old nor new file is present; a crash exactly then is the
    /// one scenario this doesn't fully protect against.
    /// </summary>
    public sealed class FilePersistenceStorage : IPersistenceStorage
    {
        private const string FileExtension = ".sav";
        private const string TempSuffix = ".tmp";

        private readonly string _rootDirectory;
        private readonly string _fileExtension;
        private readonly string _tempSuffix;

        public FilePersistenceStorage(
            string fileExtension = FileExtension,
            string tempSuffix = TempSuffix)
            : this(Path.Combine(Application.persistentDataPath, "Saves"), fileExtension, tempSuffix)
        {
        }

        public FilePersistenceStorage(
            string rootDirectory,
            string fileExtension = FileExtension,
            string tempSuffix = TempSuffix)
        {
            _rootDirectory = Guard.NotNullOrEmpty(rootDirectory, nameof(rootDirectory));
            _fileExtension = fileExtension;
            _tempSuffix = tempSuffix;
        }

        public bool Exists(string key)
        {
            return File.Exists(GetPath(key));
        }

        public string ReadText(string key)
        {
            string path = GetPath(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public void WriteText(string key, string contents)
        {
            string path = GetPath(key);
            Directory.CreateDirectory(_rootDirectory);

            string tempPath = path + _tempSuffix;
            File.WriteAllText(tempPath, contents);

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tempPath, path);
        }

        public void Delete(string key)
        {
            string path = GetPath(key);

            if (File.Exists(path))
                File.Delete(path);
        }

        private string GetPath(string key)
        {
            Guard.NotNullOrEmpty(key, nameof(key));
            return Path.Combine(_rootDirectory, key + _fileExtension);
        }
    }
}
