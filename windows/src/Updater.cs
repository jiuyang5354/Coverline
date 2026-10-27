using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Coverline
{
    // Self-update from GitHub Releases.
    //
    //  check (30 s after start, then every 6 h) -> newest release by semantic version (prereleases
    //  included, drafts skipped) -> download zip + SHA256SUMS.txt to %LocalAppData%\Coverline\updates
    //  -> verify hash -> extract to a staging folder -> when idle (nothing playing, pointer not on the
    //  widget) write a small PowerShell script that waits for this process to exit, backs the old exe
    //  up as Coverline.old.exe, copies the new files over, rolls back on failure and starts the exe.
    //
    // Everything here except Controller wiring is free of WPF/WinForms so it can be tested off Windows.

#pragma warning disable 0649 // filled by DataContractJsonSerializer
    [DataContract]
    internal sealed class ReleaseAsset
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "browser_download_url")] public string Url;
        [DataMember(Name = "size")] public long Size;
    }

    [DataContract]
    internal sealed class ReleaseInfo
    {
        [DataMember(Name = "tag_name")] public string Tag;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "draft")] public bool Draft;
        [DataMember(Name = "prerelease")] public bool Prerelease;
        [DataMember(Name = "html_url")] public string HtmlUrl;
        [DataMember(Name = "assets")] public List<ReleaseAsset> Assets;

        public ReleaseAsset Asset(string name)
        {
            if (Assets == null) return null;
            foreach (var a in Assets)
                if (a != null && string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        public string ZipName { get { return AppInfo.Name + "-Windows-" + Tag + ".zip"; } }
        public ReleaseAsset Zip { get { return Asset(ZipName); } }
        public ReleaseAsset Sums { get { return Asset("SHA256SUMS.txt"); } }
    }
#pragma warning restore 0649

    internal sealed class RateLimitedException : Exception
    {
        public readonly DateTime UntilUtc;
        public RateLimitedException(DateTime untilUtc) : base("rate limited until " + untilUtc.ToString("u")) { UntilUtc = untilUtc; }
    }

    // Pure helpers: version maths, release choice, checksum parsing, download/verify/extract.
    internal static class UpdateCore
    {
        public const string ReleasesApi = "https://api.github.com/repos/jiuyang5354/Coverline/releases?per_page=10";
        public const string ReleasesPage = "https://github.com/jiuyang5354/Coverline/releases";
        private const long MaxZipBytes = 64L * 1024 * 1024;

        // ---- semantic versions ----

        private sealed class SemVer
        {
            public int[] Numbers;
            public string[] Pre; // empty = release
        }

        private static SemVer ParseVersion(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            s = s.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            int plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus);
            string pre = "";
            int dash = s.IndexOf('-');
            if (dash >= 0) { pre = s.Substring(dash + 1); s = s.Substring(0, dash); }
            string[] parts = s.Split('.');
            if (parts.Length < 1 || parts.Length > 4) return null;
            var nums = new int[4];
            for (int i = 0; i < parts.Length; i++)
            {
                int n;
                if (parts[i].Length == 0 || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out n)) return null;
                nums[i] = n;
            }
            if (dash >= 0 && pre.Length == 0) return null;
            return new SemVer { Numbers = nums, Pre = pre.Length == 0 ? new string[0] : pre.Split('.') };
        }

        public static bool IsVersion(string s) { return ParseVersion(s) != null; }

        // <0 if a<b, 0 equal, >0 if a>b. Unparsable versions sort below everything.
        public static int CompareVersions(string a, string b)
        {
            SemVer x = ParseVersion(a), y = ParseVersion(b);
            if (x == null || y == null) return (x == null ? 0 : 1) - (y == null ? 0 : 1);
            for (int i = 0; i < 4; i++)
                if (x.Numbers[i] != y.Numbers[i]) return x.Numbers[i].CompareTo(y.Numbers[i]);
            if (x.Pre.Length == 0 || y.Pre.Length == 0) return (x.Pre.Length == 0 ? 1 : 0) - (y.Pre.Length == 0 ? 1 : 0);
            for (int i = 0; i < Math.Min(x.Pre.Length, y.Pre.Length); i++)
            {
                int p, q;
                bool pn = int.TryParse(x.Pre[i], NumberStyles.None, CultureInfo.InvariantCulture, out p);
                bool qn = int.TryParse(y.Pre[i], NumberStyles.None, CultureInfo.InvariantCulture, out q);
                int c;
                if (pn && qn) c = p.CompareTo(q);
                else if (pn) c = -1;
                else if (qn) c = 1;
                else c = string.CompareOrdinal(x.Pre[i], y.Pre[i]);
                if (c != 0) return c;
            }
            return x.Pre.Length.CompareTo(y.Pre.Length);
        }

        // ---- releases ----

        public static List<ReleaseInfo> ParseReleases(string json)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var list = new DataContractJsonSerializer(typeof(List<ReleaseInfo>)).ReadObject(ms) as List<ReleaseInfo>;
                return list ?? new List<ReleaseInfo>();
            }
        }

        // Highest semantic version among non-draft releases (prereleases count) that carry both the
        // Windows zip and SHA256SUMS.txt.
        public static ReleaseInfo PickLatest(IEnumerable<ReleaseInfo> releases)
        {
            ReleaseInfo best = null;
            foreach (var r in releases)
            {
                if (r == null || r.Draft || !IsVersion(r.Tag)) continue;
                if (r.Zip == null || r.Sums == null) continue;
                if (best == null || CompareVersions(r.Tag, best.Tag) > 0) best = r;
            }
            return best;
        }

        // "<hex>  <file>" or "<hex> *<file>" per line; returns the hash for fileName, lower-case, or null.
        public static string FindHash(string sums, string fileName)
        {
            if (sums == null) return null;
            foreach (string raw in sums.Split('\n'))
            {
                string line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int sp = line.IndexOfAny(new[] { ' ', '\t' });
                if (sp <= 0) continue;
                string hash = line.Substring(0, sp).Trim();
                string name = line.Substring(sp).Trim().TrimStart('*').Trim();
                if (hash.Length != 64 || !IsHex(hash)) continue;
                if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)) return hash.ToLowerInvariant();
            }
            return null;
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
            {
                var sb = new StringBuilder(64);
                foreach (byte b in sha.ComputeHash(f)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ---- HTTP ----

        public static void EnsureTls12()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        private static HttpWebRequest Request(string url, int timeoutMs, string accept)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = AppInfo.Name + "/" + AppInfo.Version + " (+https://" + AppInfo.Repo + ")";
            req.Accept = accept;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.AllowAutoRedirect = true;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return req;
        }

        private static WebResponse Send(HttpWebRequest req)
        {
            try { return req.GetResponse(); }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp != null && ((int)resp.StatusCode == 403 || (int)resp.StatusCode == 429))
                {
                    DateTime until = DateTime.UtcNow.AddHours(1);
                    long reset;
                    string retry = resp.Headers["Retry-After"], resetHeader = resp.Headers["X-RateLimit-Reset"];
                    int seconds;
                    if (retry != null && int.TryParse(retry, out seconds)) until = DateTime.UtcNow.AddSeconds(Math.Max(60, seconds));
                    else if (resetHeader != null && long.TryParse(resetHeader, out reset))
                        until = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(reset).AddSeconds(30);
                    resp.Close();
                    throw new RateLimitedException(until);
                }
                if (resp != null) resp.Close();
                throw;
            }
        }

        public static string GetString(string url, string accept)
        {
            var req = Request(url, 20000, accept);
            if (url.StartsWith("https://api.github.com/", StringComparison.OrdinalIgnoreCase))
                req.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            using (var resp = Send(req))
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        public static void Download(string url, string path)
        {
            string part = path + ".part";
            var req = Request(url, 30000, "application/octet-stream");
            using (var resp = Send(req))
            using (var input = resp.GetResponseStream())
            using (var output = File.Create(part))
            {
                var buffer = new byte[81920];
                long total = 0;
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += n;
                    if (total > MaxZipBytes) throw new IOException("download too large");
                    output.Write(buffer, 0, n);
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(part, path);
        }

        public static List<ReleaseInfo> FetchReleases()
        {
            return ParseReleases(GetString(ReleasesApi, "application/vnd.github+json"));
        }

        // Downloads, verifies and extracts a release into updatesDir. Returns the staging folder that
        // holds Coverline.exe, or null (with a reason) when anything does not check out.
        public static string Stage(ReleaseInfo release, string updatesDir, out string error)
        {
            error = null;
            ReleaseAsset zip = release.Zip, sumsAsset = release.Sums;
            if (zip == null || sumsAsset == null) { error = "release has no zip or SHA256SUMS.txt"; return null; }
            Directory.CreateDirectory(updatesDir);

            string expected = FindHash(GetString(sumsAsset.Url, "text/plain, application/octet-stream"), zip.Name);
            if (expected == null) { error = "no hash for " + zip.Name + " in SHA256SUMS.txt"; return null; }

            string zipPath = Path.Combine(updatesDir, zip.Name);
            if (!File.Exists(zipPath) || Sha256File(zipPath) != expected) Download(zip.Url, zipPath);
            string actual = Sha256File(zipPath);
            if (actual != expected)
            {
                TryDelete(zipPath);
                error = "hash mismatch (expected " + expected.Substring(0, 12) + "…, got " + actual.Substring(0, 12) + "…)";
                return null;
            }

            string staging = Path.Combine(updatesDir, "staging-" + release.Tag);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            string root = Path.GetFullPath(staging + Path.DirectorySeparatorChar);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.EndsWith("/")) continue;
                    string dest = Path.GetFullPath(Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar)));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { error = "unsafe path in zip: " + name; return null; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    entry.ExtractToFile(dest, true);
                }
            }
            if (!File.Exists(Path.Combine(staging, AppInfo.Name + ".exe")))
            {
                error = "zip does not contain " + AppInfo.Name + ".exe at its root";
                return null;
            }
            return staging;
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".coverline-write-test-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch { return false; }
        }

        public static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        public static string PsQuote(string s) { return "'" + s.Replace("'", "''") + "'"; }

        // The script that swaps files once this process has exited.
        public static string BuildApplyScript(int pid, string staging, string exePath, string logPath, string zipPath)
        {
            string dir = Path.GetDirectoryName(exePath);
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$procId = " + pid.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("$src = " + PsQuote(staging));
            sb.AppendLine("$dir = " + PsQuote(dir));
            sb.AppendLine("$exe = " + PsQuote(exePath));
            sb.AppendLine("$log = " + PsQuote(logPath));
            sb.AppendLine("$zip = " + PsQuote(zipPath));
            sb.AppendLine("$newExeName = " + PsQuote(AppInfo.Name + ".exe"));
            sb.AppendLine("$oldExe = Join-Path $dir " + PsQuote(AppInfo.Name + ".old.exe"));
            sb.AppendLine("$bak = $src + '.backup'");
            sb.Append(@"function L($m) { try { Add-Content -LiteralPath $log -Value ((Get-Date -Format s) + ' [apply] ' + $m) -Encoding UTF8 } catch { } }
function CopyRetry($from, $to) {
  for ($i = 0; $i -lt 10; $i++) {
    try { Copy-Item -LiteralPath $from -Destination $to -Force; return } catch { if ($i -eq 9) { throw }; Start-Sleep -Milliseconds 500 }
  }
}
$deadline = (Get-Date).AddSeconds(60)
while (Get-Process -Id $procId -ErrorAction SilentlyContinue) {
  if ((Get-Date) -gt $deadline) { L 'old process did not exit; giving up'; exit 1 }
  Start-Sleep -Milliseconds 250
}
Start-Sleep -Milliseconds 300
$files = @(Get-ChildItem -LiteralPath $src -File)
$done = @()
$ok = $false
$exeTouched = $false
try {
  New-Item -ItemType Directory -Force -Path $bak | Out-Null
  CopyRetry $exe $oldExe
  foreach ($f in $files) {
    $name = if ($f.Name -eq $newExeName) { Split-Path $exe -Leaf } else { $f.Name }
    $t = Join-Path $dir $name
    if ((Test-Path -LiteralPath $t) -and ($t -ne $exe)) { Copy-Item -LiteralPath $t -Destination (Join-Path $bak $name) -Force }
  }
  foreach ($f in $files) {
    $name = if ($f.Name -eq $newExeName) { Split-Path $exe -Leaf } else { $f.Name }
    $t = Join-Path $dir $name
    $existed = Test-Path -LiteralPath $t
    $done += ,@($t, $existed, $name)
    if ($t -eq $exe) { $exeTouched = $true }
    CopyRetry $f.FullName $t
  }
  $ok = $true
  L ('copied ' + $files.Count + ' files into ' + $dir)
} catch {
  L ('copy failed: ' + $_.Exception.Message + '; rolling back')
  foreach ($d in $done) {
    try {
      if ($d[0] -eq $exe) { }
      elseif ($d[1]) { Copy-Item -LiteralPath (Join-Path $bak $d[2]) -Destination $d[0] -Force }
      else { Remove-Item -LiteralPath $d[0] -Force }
    } catch { L ('rollback of ' + $d[2] + ' failed: ' + $_.Exception.Message) }
  }
  try { if ($exeTouched) { CopyRetry $oldExe $exe } } catch { L ('exe rollback failed: ' + $_.Exception.Message) }
}
try { Start-Process -FilePath $exe -WorkingDirectory $dir } catch { L ('restart failed: ' + $_.Exception.Message) }
foreach ($p in @($src, $bak)) { try { Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue } catch { } }
if ($ok) { try { Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue } catch { } }
try { Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue } catch { }
");
            return sb.ToString();
        }
    }

    internal static class UpdateLog
    {
        public static string Root
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name); }
        }

        public static string FilePath { get { return Path.Combine(Root, "update.log"); } }

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Root);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 128 * 1024) info.Delete();
                File.AppendAllText(FilePath, DateTime.Now.ToString("s") + " " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    // The scheduler. All events and the Func callbacks run on the UI thread (through `post`);
    // network and disk work runs on the thread pool.
    internal sealed class Updater : IDisposable
    {
        private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
        private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(30);

        private readonly Action<Action> post;
        private Timer checkTimer;
        private Timer idleTimer;
        private int busy;
        private DateTime rateLimitedUntil = DateTime.MinValue;
        private string readOnlyNotified;
        private ReleaseInfo stagedRelease;
        private string stagedDir;
        private bool applying;

        public Func<bool> IsIdle = delegate { return true; };
        public Func<bool> AutoEnabled = delegate { return true; };
        public event Action<string, Action> Notify;   // text, optional click action
        public event Action StateChanged;
        public event Action ExitRequested;

        public Updater(Action<Action> post)
        {
            this.post = post;
            UpdateCore.EnsureTls12();
        }

        public static string UpdatesDir { get { return Path.Combine(UpdateLog.Root, "updates"); } }
        private static string MarkerPath { get { return Path.Combine(UpdatesDir, "pending.txt"); } }
        public static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }

        public string ReadyVersion { get { return stagedRelease != null ? stagedRelease.Tag : null; } }
        public bool Busy { get { return busy != 0; } }

        public void Start()
        {
            FinishPreviousUpdate();
            checkTimer = new Timer(delegate { post(delegate { if (AutoEnabled()) Check(false); }); }, null, FirstDelay, Interval);
            idleTimer = new Timer(delegate { post(InstallIfIdle); }, null, IdlePoll, IdlePoll);
        }

        // First run after an update: announce it and clean up.
        private void FinishPreviousUpdate()
        {
            try
            {
                if (!File.Exists(MarkerPath)) return;
                string target = File.ReadAllText(MarkerPath).Trim();
                File.Delete(MarkerPath);
                if (UpdateCore.CompareVersions(target, AppInfo.Version) == 0)
                {
                    UpdateLog.Write("now running " + AppInfo.Version + " (updated)");
                    string old = Path.Combine(Path.GetDirectoryName(ExePath), AppInfo.Name + ".old.exe");
                    UpdateCore.TryDelete(old);
                    post(delegate { Raise("已更新到 v" + AppInfo.Version + "  Updated to v" + AppInfo.Version, null); });
                }
                else
                {
                    UpdateLog.Write("update to " + target + " did not take; still on " + AppInfo.Version);
                }
            }
            catch (Exception ex) { UpdateLog.Write("finish: " + ex.Message); }
        }

        // Call on the UI thread. manual = from the tray: reports every outcome in a balloon.
        public void Check(bool manual)
        {
            if (applying) return;
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            {
                if (manual) Raise("正在检查更新… Checking for updates…", null);
                return;
            }
            if (manual) Raise("正在检查更新… Checking for updates…", null);
            ThreadPool.QueueUserWorkItem(delegate { Work(manual); });
        }

        private void Work(bool manual)
        {
            try
            {
                if (DateTime.UtcNow < rateLimitedUntil)
                {
                    if (manual) Report("GitHub 暂时限制了访问，请稍后再试。GitHub rate limit — try again later.", null);
                    return;
                }
                var releases = UpdateCore.FetchReleases();
                var latest = UpdateCore.PickLatest(releases);
                if (latest == null || UpdateCore.CompareVersions(latest.Tag, AppInfo.Version) <= 0)
                {
                    UpdateLog.Write("check: up to date (" + AppInfo.Version + ")");
                    if (manual) Report("已是最新版本 v" + AppInfo.Version + "  You're up to date.", null);
                    return;
                }
                if (stagedRelease != null && UpdateCore.CompareVersions(stagedRelease.Tag, latest.Tag) >= 0)
                {
                    if (manual) Report(latest.Tag + " 已下载，点此立即重启更新。Click to restart and update.", ApplyNow);
                    return;
                }
                UpdateLog.Write("check: " + latest.Tag + " available (running " + AppInfo.Version + ")");

                string installDir = Path.GetDirectoryName(ExePath);
                if (!UpdateCore.CanWrite(installDir))
                {
                    UpdateLog.Write("install folder not writable: " + installDir);
                    if (manual || readOnlyNotified != latest.Tag)
                    {
                        readOnlyNotified = latest.Tag;
                        string page = string.IsNullOrEmpty(latest.HtmlUrl) ? UpdateCore.ReleasesPage : latest.HtmlUrl;
                        Report("发现 " + latest.Tag + "，但安装目录不可写，请手动下载。" + latest.Tag + " is out; the install folder is read-only, please update manually.",
                            delegate { OpenUrl(page); });
                        OpenUrl(page);
                    }
                    return;
                }

                if (manual) Report("发现 " + latest.Tag + "，正在后台下载… Downloading " + latest.Tag + "…", null);
                string error;
                string staging = UpdateCore.Stage(latest, UpdatesDir, out error);
                if (staging == null)
                {
                    UpdateLog.Write("stage " + latest.Tag + " abandoned: " + error);
                    if (manual) Report("更新包校验失败，已放弃。Update check failed: " + error, null);
                    return;
                }
                UpdateLog.Write("staged " + latest.Tag + " (sha256 ok)");
                post(delegate
                {
                    stagedRelease = latest;
                    stagedDir = staging;
                    if (StateChanged != null) StateChanged();
                    if (manual || !AutoEnabled()) Raise(latest.Tag + " 已就绪，点此立即重启更新。Ready — click to restart and update.", ApplyNow);
                    InstallIfIdle();
                });
            }
            catch (RateLimitedException ex)
            {
                rateLimitedUntil = ex.UntilUtc;
                UpdateLog.Write("check: " + ex.Message);
                if (manual) Report("GitHub 暂时限制了访问，请稍后再试。GitHub rate limit — try again later.", null);
            }
            catch (Exception ex)
            {
                UpdateLog.Write("check failed: " + ex.GetType().Name + ": " + ex.Message);
                if (manual) Report("检查更新失败（网络不可用？）Couldn't check for updates.", null);
            }
            finally
            {
                Interlocked.Exchange(ref busy, 0);
            }
        }

        private void InstallIfIdle()
        {
            if (stagedRelease == null || applying || !AutoEnabled()) return;
            bool idle;
            try { idle = IsIdle(); } catch { idle = false; }
            if (idle) ApplyNow();
        }

        // UI thread. Hands over to the PowerShell script and asks the app to quit.
        public void ApplyNow()
        {
            if (stagedRelease == null || applying) return;
            try
            {
                if (!File.Exists(Path.Combine(stagedDir, AppInfo.Name + ".exe"))) throw new IOException("staged files are gone");
                Directory.CreateDirectory(UpdatesDir);
                string script = Path.Combine(UpdatesDir, "apply-" + stagedRelease.Tag + ".ps1");
                string zip = Path.Combine(UpdatesDir, stagedRelease.ZipName);
                File.WriteAllText(script, UpdateCore.BuildApplyScript(Process.GetCurrentProcess().Id, stagedDir, ExePath, UpdateLog.FilePath, zip), new UTF8Encoding(true));
                File.WriteAllText(MarkerPath, stagedRelease.Tag);

                string ps = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                if (!File.Exists(ps)) ps = "powershell.exe";
                var psi = new ProcessStartInfo(ps, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = UpdatesDir
                };
                Process.Start(psi);
                applying = true;
                UpdateLog.Write("applying " + stagedRelease.Tag + "; restarting");
                if (ExitRequested != null) ExitRequested();
            }
            catch (Exception ex)
            {
                UpdateLog.Write("apply failed to start: " + ex.Message);
                UpdateCore.TryDelete(MarkerPath);
                stagedRelease = null;
                stagedDir = null;
                if (StateChanged != null) StateChanged();
                Raise("无法安装更新，请从 Releases 页面手动下载。Couldn't install the update.", delegate { OpenUrl(UpdateCore.ReleasesPage); });
            }
        }

        private void Report(string text, Action click) { post(delegate { Raise(text, click); }); }

        private void Raise(string text, Action click) { if (Notify != null) Notify(text, click); }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { UpdateLog.Write("open " + url + ": " + ex.Message); }
        }

        public void Dispose()
        {
            if (checkTimer != null) checkTimer.Dispose();
            if (idleTimer != null) idleTimer.Dispose();
        }
    }
}
