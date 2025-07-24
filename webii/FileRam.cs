using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace webii
{
    public class FileRam
    {
        // Używanie pojemności początkowej dla słowników poprawia wydajność przy dużej ilości plików
        private readonly Dictionary<string, byte[]> _cache = new(64);
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new(64);
        private readonly Dictionary<string, DateTime> _lastModified = new(64);
        private readonly Dictionary<string, DateTime> _lastAccessed = new(64);
        private readonly long _maxCacheBytes;
        private long _currentCacheBytes;
        private readonly object _lock = new();

        public FileRam(long maxCacheBytes = 1024 * 1024) // Default to 100 MB
        {
            _maxCacheBytes = maxCacheBytes;
            _currentCacheBytes = 0;
        }

        public byte[]? GetBytes(string path)
        {
            Console.WriteLine($"[CACHE] GetBytes called for: {path}");
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var data))
                {
                    _lastAccessed[path] = DateTime.UtcNow;
                    return data;
                }

                if (!File.Exists(path))
                    return null;

                var bytes = File.ReadAllBytes(path);
                var bytesLength = bytes.Length;

                // Ensure we have space for the new file
                EnsureCacheSpace(bytesLength);

                _cache[path] = bytes;
                _lastModified[path] = File.GetLastWriteTimeUtc(path);
                _lastAccessed[path] = DateTime.UtcNow;
                _currentCacheBytes += bytesLength;
                Console.WriteLine($"[CACHE] GetBytes from file: {path} ({bytesLength} bytes), current cache size: {_currentCacheBytes} bytes");
                SetupWatcher(path);
                return bytes;
            }
        }

        public DateTime? GetLastModified(string path)
        {
            lock (_lock)
            {
                if (_lastModified.TryGetValue(path, out var lastMod))
                {
                    Console.WriteLine($"[CACHE] GetLastModified from cache: {path} = {lastMod:R}");
                    return lastMod;
                }

                if (File.Exists(path))
                {
                    var lastModified = File.GetLastWriteTimeUtc(path);
                    _lastModified[path] = lastModified;
                    Console.WriteLine($"[CACHE] GetLastModified from file: {path} = {lastModified:R}");
                    return lastModified;
                }

                Console.WriteLine($"[CACHE] GetLastModified file not found: {path}");
                return null;
            }
        }

        public string? GetETag(string path)
        {
            var lastModified = GetLastModified(path);
            if (lastModified.HasValue)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    // Używamy stałych formatów dla poprawy wydajności
                    return $"\"{path.GetHashCode():X8}-{fileInfo.Length:X8}-{lastModified.Value.Ticks:X16}\"";
                }
            }
            return null;
        }

        public string? GetText(string path, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = GetBytes(path);
            return bytes != null ? encoding.GetString(bytes) : null;
        }

        private void SetupWatcher(string path)
        {
            if (_watchers.ContainsKey(path))
                return;

            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directoryPath))
                return;

            var watcher = new FileSystemWatcher
            {
                Path = directoryPath,
                Filter = Path.GetFileName(path),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            // Używamy jednego handlera dla wszystkich zdarzeń
            FileSystemEventHandler handler = (s, e) => Invalidate(path);
            watcher.Changed += handler;
            watcher.Deleted += handler;
            watcher.Renamed += (s, e) => Invalidate(path);
            watcher.EnableRaisingEvents = true;

            _watchers[path] = watcher;
        }

        private void Invalidate(string path)
        {
            lock (_lock)
            {
                var hadCache = _cache.ContainsKey(path);
                var hadLastModified = _lastModified.ContainsKey(path);

                if (_cache.TryGetValue(path, out var data))
                {
                    _currentCacheBytes -= data.Length;
                }

                _cache.Remove(path);
                _lastModified.Remove(path);
                _lastAccessed.Remove(path);

                if (_watchers.TryGetValue(path, out var watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    _watchers.Remove(path);
                }

                Console.WriteLine($"[CACHE] Cache invalidated for: {path} (had cache: {hadCache}, had lastModified: {hadLastModified})");
            }
        }

        private void EnsureCacheSpace(long requiredBytes)
        {
            // Unikamy niepotrzebnej alokacji pamięci i obliczeń, jeśli mamy wystarczająco dużo miejsca
            if (_currentCacheBytes + requiredBytes <= _maxCacheBytes || _cache.Count == 0)
                return;

            Console.WriteLine($"[CACHE] Ensuring cache space for: {requiredBytes} bytes");

            while (_currentCacheBytes + requiredBytes > _maxCacheBytes && _cache.Count > 0)
            {
                // Znajdź najstarszy element bez użycia LINQ
                DateTime oldestTime = DateTime.MaxValue;
                string oldestPath = null!;

                foreach (var entry in _lastAccessed)
                {
                    if (_cache.ContainsKey(entry.Key) && entry.Value < oldestTime)
                    {
                        oldestTime = entry.Value;
                        oldestPath = entry.Key;
                    }
                }

                if (oldestPath == null)
                    break;
                Console.BackgroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[CACHE] Evicting oldest entry: {oldestPath} (accessed: {oldestTime:R})");
                Console.BackgroundColor = ConsoleColor.Black;

                // Usuń najstarszy element
                if (_cache.TryGetValue(oldestPath, out var data))
                {
                    _currentCacheBytes -= data.Length;
                }

                _cache.Remove(oldestPath);
                _lastModified.Remove(oldestPath);
                _lastAccessed.Remove(oldestPath);

                if (_watchers.TryGetValue(oldestPath, out var watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    _watchers.Remove(oldestPath);
                }
            }
        }
    }
}