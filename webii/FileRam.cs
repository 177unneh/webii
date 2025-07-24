using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace webii
{
    public class FileRam
    {
        

        public FileRam(long maxCacheBytes = 1024 * 1024 * 100) // Default to 100 MB
        {
        }
        private readonly Dictionary<string, byte[]> _cache = new();
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly object _lock = new();

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

                SetupWatcher(path);
                return bytes;
            }
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
                _cache.Remove(path);

                if (_watchers.TryGetValue(path, out var watcher))
                {
                    watcher.Dispose();
                    _watchers.Remove(path);
                }
            }

            Console.WriteLine($"[cache] invalidated: {path}");
        }
    }
}
