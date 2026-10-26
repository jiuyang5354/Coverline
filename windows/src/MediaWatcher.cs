using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Coverline
{
    // A snapshot of what is playing. Immutable once published.
    internal sealed class NowPlaying
    {
        public static readonly NowPlaying Empty = new NowPlaying();

        public bool HasSession;
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public string Source = "";
        public bool IsPlaying;
        public bool CanPlayPause;
        public bool CanNext;
        public bool CanPrevious;
        public BitmapSource Art;
        public string ArtKey = "";

        public string TextKey
        {
            get { return HasSession ? Title + "\u001f" + Artist : ""; }
        }
    }

    // Reads the current track from Windows' System Media Transport Controls (the same source the
    // volume flyout and lock screen use), so any app that reports media works: Spotify, Apple Music,
    // browsers, foobar2000 with the SMTC plugin, and so on.
    internal sealed class MediaWatcher : IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly DispatcherTimer poll;
        private GlobalSystemMediaTransportControlsSessionManager manager;
        private GlobalSystemMediaTransportControlsSession session;
        private string sessionId = "";
        private NowPlaying current = NowPlaying.Empty;
        private int generation;
        private bool disposed;

        private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, CurrentSessionChangedEventArgs> onCurrentChanged;
        private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, SessionsChangedEventArgs> onSessionsChanged;
        private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs> onPropsChanged;
        private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs> onPlaybackChanged;

        public event Action<NowPlaying> Changed;

        public MediaWatcher(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            onCurrentChanged = delegate { Post(new Action(Reselect)); };
            onSessionsChanged = delegate { Post(new Action(Reselect)); };
            onPropsChanged = delegate { Post(new Action(RefreshAll)); };
            onPlaybackChanged = delegate { Post(new Action(RefreshPlayback)); };

            // Some players report changes late or not at all; a slow poll keeps the widget honest.
            poll = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(3) };
            poll.Tick += delegate { Reselect(); };
        }

        public NowPlaying Current { get { return current; } }

        public async void Start()
        {
            try
            {
                manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            catch (Exception ex)
            {
                // Windows older than 10 1809, or the media service is unavailable.
                Log.Write("SMTC unavailable: " + ex.Message);
                Publish(NowPlaying.Empty);
                return;
            }
            if (disposed) return;
            manager.CurrentSessionChanged += onCurrentChanged;
            manager.SessionsChanged += onSessionsChanged;
            Reselect();
            poll.Start();
        }

        private void Post(Action action)
        {
            if (!disposed) dispatcher.BeginInvoke(action);
        }

        // Picks the session to show: the one Windows calls current, else one that is playing,
        // else any session at all.
        private void Reselect()
        {
            if (manager == null || disposed) return;
            GlobalSystemMediaTransportControlsSession pick = null;
            try
            {
                pick = manager.GetCurrentSession();
                if (pick == null)
                {
                    var all = manager.GetSessions().ToList();
                    pick = all.FirstOrDefault(s => IsPlaying(s)) ?? all.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Log.Write("session lookup: " + ex.Message);
            }

            string id = pick == null ? "" : SafeId(pick);
            if (pick != null && session != null && id == sessionId)
            {
                RefreshPlayback();
                return;
            }

            Detach();
            session = pick;
            sessionId = id;
            if (session == null)
            {
                Publish(NowPlaying.Empty);
                return;
            }
            try
            {
                session.MediaPropertiesChanged += onPropsChanged;
                session.PlaybackInfoChanged += onPlaybackChanged;
            }
            catch (Exception ex)
            {
                Log.Write("session subscribe: " + ex.Message);
            }
            RefreshAll();
        }

        private void Detach()
        {
            if (session == null) return;
            try
            {
                session.MediaPropertiesChanged -= onPropsChanged;
                session.PlaybackInfoChanged -= onPlaybackChanged;
            }
            catch { }
            session = null;
            sessionId = "";
        }

        private static bool IsPlaying(GlobalSystemMediaTransportControlsSession s)
        {
            try
            {
                var info = s.GetPlaybackInfo();
                return info != null && info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            }
            catch { return false; }
        }

        private static string SafeId(GlobalSystemMediaTransportControlsSession s)
        {
            try { return s.SourceAppUserModelId ?? ""; }
            catch { return ""; }
        }

        private void RefreshPlayback()
        {
            if (session == null || !current.HasSession) return;
            var next = Clone(current);
            ReadPlayback(session, next);
            if (next.IsPlaying != current.IsPlaying || next.CanNext != current.CanNext ||
                next.CanPrevious != current.CanPrevious || next.CanPlayPause != current.CanPlayPause)
                Publish(next);
        }

        private async void RefreshAll()
        {
            var s = session;
            if (s == null) return;
            int gen = ++generation;
            var next = new NowPlaying { HasSession = true, Source = FriendlySource(SafeId(s)) };
            try
            {
                var props = await s.TryGetMediaPropertiesAsync();
                if (props != null)
                {
                    next.Title = props.Title ?? "";
                    next.Artist = !string.IsNullOrEmpty(props.Artist) ? props.Artist : (props.AlbumArtist ?? "");
                    next.Album = props.AlbumTitle ?? "";
                    if (props.Thumbnail != null)
                    {
                        byte[] bytes = await ReadAll(props.Thumbnail);
                        if (bytes != null && bytes.Length > 0)
                        {
                            next.ArtKey = Hash(bytes);
                            next.Art = next.ArtKey == current.ArtKey && current.Art != null ? current.Art : Decode(bytes);
                            if (next.Art == null) next.ArtKey = "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("media properties: " + ex.Message);
            }
            if (gen != generation || s != session || disposed) return; // a newer refresh has started
            ReadPlayback(s, next);
            Publish(next);
        }

        private static void ReadPlayback(GlobalSystemMediaTransportControlsSession s, NowPlaying target)
        {
            try
            {
                var info = s.GetPlaybackInfo();
                if (info == null) return;
                target.IsPlaying = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var c = info.Controls;
                if (c != null)
                {
                    target.CanPlayPause = c.IsPlayPauseToggleEnabled || c.IsPlayEnabled || c.IsPauseEnabled;
                    target.CanNext = c.IsNextEnabled;
                    target.CanPrevious = c.IsPreviousEnabled;
                }
            }
            catch { }
        }

        private static async Task<byte[]> ReadAll(IRandomAccessStreamReference reference)
        {
            using (IRandomAccessStreamWithContentType ras = await reference.OpenReadAsync())
            using (Stream stream = ras.AsStreamForRead())
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                return buffer.ToArray();
            }
        }

        private static BitmapSource Decode(byte[] bytes)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.DecodePixelWidth = 600;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                Log.Write("art decode: " + ex.Message);
                return null;
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (var md5 = MD5.Create())
                return Convert.ToBase64String(md5.ComputeHash(bytes));
        }

        // "Spotify.exe" or "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" -> "Spotify"
        private static string FriendlySource(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string s = id;
            int bang = s.LastIndexOf('!');
            if (bang >= 0 && bang < s.Length - 1) s = s.Substring(bang + 1);
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            int dot = s.LastIndexOf('.');
            if (dot >= 0 && dot < s.Length - 1) s = s.Substring(dot + 1);
            if (s.Equals("msedge", StringComparison.OrdinalIgnoreCase)) return "Edge";
            if (s.Equals("chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
            if (s.Equals("firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
            if (s.Equals("AppleMusic", StringComparison.OrdinalIgnoreCase)) return "Apple Music";
            return s;
        }

        private static NowPlaying Clone(NowPlaying n)
        {
            return new NowPlaying
            {
                HasSession = n.HasSession, Title = n.Title, Artist = n.Artist, Album = n.Album, Source = n.Source,
                IsPlaying = n.IsPlaying, CanPlayPause = n.CanPlayPause, CanNext = n.CanNext, CanPrevious = n.CanPrevious,
                Art = n.Art, ArtKey = n.ArtKey
            };
        }

        private void Publish(NowPlaying next)
        {
            current = next;
            var handler = Changed;
            if (handler != null) handler(next);
        }

        public async void PlayPause()
        {
            var s = session;
            if (s == null) return;
            try { await s.TryTogglePlayPauseAsync(); }
            catch (Exception ex) { Log.Write("play/pause: " + ex.Message); }
        }

        public async void Next()
        {
            var s = session;
            if (s == null) return;
            try { await s.TrySkipNextAsync(); }
            catch (Exception ex) { Log.Write("next: " + ex.Message); }
        }

        public async void Previous()
        {
            var s = session;
            if (s == null) return;
            try { await s.TrySkipPreviousAsync(); }
            catch (Exception ex) { Log.Write("previous: " + ex.Message); }
        }

        public void Dispose()
        {
            disposed = true;
            poll.Stop();
            Detach();
            if (manager != null)
            {
                try
                {
                    manager.CurrentSessionChanged -= onCurrentChanged;
                    manager.SessionsChanged -= onSessionsChanged;
                }
                catch { }
            }
        }
    }
}
