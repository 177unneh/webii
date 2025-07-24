using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace webii
{
    public class FileRam
    {
        private readonly Dictionary<string, byte[]> _cache = new();
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly Dictionary<string, DateTime> _lastModified = new();
        private readonly object _lock = new();

        public FileRam(long maxCacheBytes = 1024 * 1024 * 100) // Default to 100 MB
        {
        }

        public byte[] GetBytes(string path)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var data))
                    return data;

                if (!File.Exists(path))
                    return null;

                var bytes = File.ReadAllBytes(path);
                _cache[path] = bytes;
                _lastModified[path] = File.GetLastWriteTimeUtc(path);

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
        public string GetETag(string path)
        {
            var lastModified = GetLastModified(path);
            if (lastModified.HasValue)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    // Generate ETag based on file path, size and last modified time
                    var etag = $"\"{path.GetHashCode():X}-{fileInfo.Length:X}-{lastModified.Value.Ticks:X}\"";
                    return etag;
                }
            }
            return null;
        }

        public string GetText(string path, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = GetBytes(path);
            return bytes != null ? encoding.GetString(bytes) : null;
        }

        private void SetupWatcher(string path)
        {
            if (_watchers.ContainsKey(path))
                return;

            var watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(path),
                Filter = Path.GetFileName(path),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Changed += (s, e) => Invalidate(path);
            watcher.Deleted += (s, e) => Invalidate(path);
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

                _cache.Remove(path);
                _lastModified.Remove(path);

                if (_watchers.TryGetValue(path, out var watcher))
                {
                    watcher.Dispose();
                    _watchers.Remove(path);
                }

                Console.WriteLine($"[CACHE] Cache invalidated for: {path} (had cache: {hadCache}, had lastModified: {hadLastModified})");
            }
        }
    }
}