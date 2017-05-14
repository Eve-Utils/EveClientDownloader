using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using log4net;
using Newtonsoft.Json.Linq;

namespace EveUtils.ClientDownloader
{
    public class EveClientDownloader
    {
        private static readonly Dictionary<EveServer, string> Abbreviation = new Dictionary<EveServer, string>();
        private static readonly MD5 Md5 = MD5.Create();
        private static readonly ILog Log = LogManager.GetLogger(typeof(EveClientDownloader));

        private readonly HttpClient _httpClient;

        private string BuildUrl { get; }
        private string BinaryIndexUrl => string.Format("https://binaries.eveonline.com/eveonline_{0}.txt", Build);

        static EveClientDownloader()
        {
            Abbreviation.Add(EveServer.Tranquility, "TQ");
            Abbreviation.Add(EveServer.Singularity, "SISI");
            Abbreviation.Add(EveServer.Chaos, "CHAOS");
            Abbreviation.Add(EveServer.Duality, "DUALITY");
            Abbreviation.Add(EveServer.Thunderdome, "THUNDERDOME");
        }

        public EveServer Server { get; }

        public EveClientDownloader(EveServer server)
        {
            Server = server;

            HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
            };
            _httpClient = new HttpClient(handler, true);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "EveClientDownloader/1.0 (https://github.com/Eve-Utils/EveClientDownloader)");

            BuildUrl = string.Format("https://binaries.eveonline.com/eveclient_{0}.json", Abbreviation[Server]);
        }

        ~EveClientDownloader()
        {
            _httpClient.Dispose();
        }

        private int? _build;
        public int Build
        {
            get
            {
                if (!_build.HasValue)
                {
                    using (HttpResponseMessage resp = _httpClient.GetAsync(BuildUrl).Result)
                    {
                        JObject root = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
                        _build = int.Parse(root["build"].Value<string>());
                    }
                }
                return _build.Value;
            }
        }

        private BinaryIndexEntry[] _binaryIndex;

        private IEnumerable<BinaryIndexEntry> BinaryIndex
        {
            get
            {
                if (_binaryIndex == null)
                {
                    using (HttpResponseMessage resp = _httpClient.GetAsync(BinaryIndexUrl).Result)
                    {
                        string[] entries = resp.Content.ReadAsStringAsync().Result.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        List<BinaryIndexEntry> list = new List<BinaryIndexEntry>(entries.Length);

                        foreach (string entry in entries)
                        {
                            list.Add(new BinaryIndexEntry(entry));
                        }

                        _binaryIndex = list.ToArray();
                    }
                }
                return _binaryIndex;
            }
        }

        public void Fetch(string targetDir, string cacheDir = null)
        {
            foreach (BinaryIndexEntry entry in BinaryIndex)
            {
                string path = Path.Combine(targetDir, entry.Path);

                if (!FileExistsMd5(path, entry.Md5Hash))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(path)))
                        Directory.CreateDirectory(Path.GetDirectoryName(path));

                    Log.Info(string.Format("Downloading {0}...", entry.Path));

                    if (cacheDir != null && FileExistsMd5(Path.Combine(cacheDir, entry.Path), entry.Md5Hash))
                    {
                        Log.Info(string.Format("Cached: {0}", entry.Path));
                        File.Copy(Path.Combine(cacheDir, entry.Path), path);
                        continue;
                    }

                    using (HttpResponseMessage resp = _httpClient.GetAsync(entry.Url).Result)
                    {
                        byte[] bytes = resp.Content.ReadAsByteArrayAsync().Result;
                        string actualHash = BitConverter.ToString(Md5.ComputeHash(bytes)).Replace("-", "");

                        if (string.Equals(entry.Md5Hash, actualHash, StringComparison.OrdinalIgnoreCase))
                            File.WriteAllBytes(path, bytes);
                        else
                            throw new InvalidDataException(string.Format("Checksum error: {0}", entry.Path));
                    }
                }
                else
                {
                    Log.Info(string.Format("Already downloaded: {0}", entry.Path));
                }
            }
        }

        private static bool FileExistsMd5(string path, string expectedHash)
        {
            if (!File.Exists(path))
                return false;

            using (FileStream stream = File.OpenRead(path))
            {
                string actualHash = BitConverter.ToString(Md5.ComputeHash(stream)).Replace("-", "");
                return string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
            }
        }

        public enum EveServer
        {
            Tranquility,
            Singularity,
            Chaos,
            Duality,
            Thunderdome
        }

        private struct BinaryIndexEntry
        {
            public string Path { get; }
            public string Url { get; }
            public string Md5Hash { get; }

            public BinaryIndexEntry(string line)
            {
                string[] parts = line.Split(',');

                Path = parts[0];
                if (Path.StartsWith("app:"))
                    Path = Path.Substring(4);
                if (Path.StartsWith("/"))
                    Path = Path.Substring(1);

                Path = Path.Replace('/', System.IO.Path.DirectorySeparatorChar);

                Url = string.Format("https://binaries.eveonline.com/{0}", parts[1]);
                Md5Hash = parts[2];
            }
        }
    }
}
