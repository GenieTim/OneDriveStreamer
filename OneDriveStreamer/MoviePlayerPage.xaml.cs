using Microsoft.OneDrive.Sdk;
using Microsoft.Services.Store.Engagement;
using MimeTypes;
using OneDriveStreamer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel.Resources;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace OneDriveStreamer
{
    /// <summary>
    /// The one page where the movie is played
    /// </summary>
    public sealed partial class MoviePlayerPage : Page
    {
        private List<string> pathComponents = new List<string>();
        private OneDriveClient oneDriveClient;
        private DisplayRequest displayRequest;
        private DateTimeOffset dlLinkAge;
        private ResourceLoader loader;

        public MoviePlayerPage()
        {
            InitializeComponent();

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested +=
                SystemNavigationManager_BackRequested;
            //
            KeepDisplayOn();
            // start function to update source after one hour (lifetime of OneDrive download link)
            var startTimeSpan = TimeSpan.Zero;
            var periodTimeSpan = TimeSpan.FromMinutes(5);

            var timer = new System.Threading.Timer((e) =>
            {
                updateMovieSourceIfNecessary();
            }, null, startTimeSpan, periodTimeSpan);

            StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
            logger.Log("moviePlayerPage");

            // 
            var coreWindow = Window.Current.CoreWindow;
            coreWindow.CharacterReceived += CoreWindow_CharacterReceived;
            //
            loader = ResourceLoader.GetForCurrentView();
        }

        private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState.Equals(MediaPlaybackState.Paused))
            {
                AllowDisplayOff();
            }
            else
            {
                KeepDisplayOn();
            }
            if (sender.PlaybackState.Equals(MediaPlaybackState.Buffering))
            {
                updateMovieSourceIfNecessary();
            }
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            AllowDisplayOff();
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            AllowDisplayOff();
        }

        private void KeepDisplayOn()
        {
            if (displayRequest == null)
            {
                displayRequest = new DisplayRequest();
            }
            // request active display
            try
            {
                displayRequest.RequestActive();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Error requesting active display: ", e);
            }
        }
        private void AllowDisplayOff()
        {
            try
            {
                displayRequest.RequestRelease();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Error requesting active display release: ", e);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameters = (VideoNavigationParameter)e.Parameter;
            oneDriveClient = parameters.oneDriveClient;
            pathComponents = parameters.PathComponents;
            setVideoSourceKeepingPlaytime(true);
        }
        private void initializeMovie(IUICommand c)
        {
            setVideoSourceKeepingPlaytime(false);
        }

        private void updateMovieSourceIfNecessary()
        {
            if (dlLinkAge != null && DateTimeOffset.UtcNow.CompareTo(dlLinkAge.AddMinutes(55)) < 0)
            {
                // it expires within 5 min -> reload
                setVideoSourceKeepingPlaytime(false);
            }
        }

        private async void setVideoSourceKeepingPlaytime(bool newMovie)
        {
            var videoPath = "/" + string.Join("/", pathComponents);
            dlLinkAge = DateTimeOffset.UtcNow;
            try
            {
                progress.Visibility = Visibility.Visible;
                TimeSpan currentPlaytime = new TimeSpan(0);
                if (!newMovie)
                {
                    try
                    {
                        currentPlaytime = mediaPlayer.MediaPlayer.PlaybackSession.Position;
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine("Error fetching current playtime: ", e);
                    }
                }
                var builder = oneDriveClient.Drive.Root.ItemWithPath(videoPath);
                var file = await builder.Request().GetAsync();
                string mimeType = MimeTypeMap.GetMimeType(file.Name);
                System.Diagnostics.Debug.WriteLine("Playing item with mime type: " + mimeType);
                object downloadUrl;
                if (file.AdditionalData.TryGetValue("@content.downloadUrl", out downloadUrl))
                {
                    var options = new Dictionary<string, object>();
                    mediaPlayer.Source = MediaSource.CreateFromUri(new Uri((string)downloadUrl));
                }
                else
                {
                    // TODO: this stream can lead to out of memory exceptions.
                    // might want to try also the VLC movie element again?
                    Stream contentStream = await builder.Content.Request().GetAsync();
                    mediaPlayer.Source = MediaSource.CreateFromStream(contentStream.AsRandomAccessStream(), mimeType);
                }
                progress.Visibility = Visibility.Collapsed;
                mediaPlayer.MediaPlayer.PlaybackSession.Position = currentPlaytime;

                // setup listeners
                if (!newMovie)
                {
                    mediaPlayer.MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                    mediaPlayer.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
                    mediaPlayer.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
                }
            }
            catch (Exception ex)
            {
                ExitOrRetryWithMessage("Failed to load movie. Error: " + ex.ToString());
            }
        }

        private async void ExitOrRetryWithMessage(string message)
        {
            // Create the message dialog and set its content
            var messageDialog = new MessageDialog(message);

            // Add commands and set their callbacks; both buttons use the same callback function instead of inline event handlers
            messageDialog.Commands.Add(new UICommand(
                 loader.GetString("Error/TryAgain"),
                new UICommandInvokedHandler(initializeMovie)));
            messageDialog.Commands.Add(new UICommand(
                loader.GetString("Error/BackToMain"),
                new UICommandInvokedHandler(ExitPlayer)));

            // Set the command that will be invoked by default
            messageDialog.DefaultCommandIndex = 0;

            // Set the command to be invoked when escape is pressed
            messageDialog.CancelCommandIndex = 1;

            // Show the message dialog
            await messageDialog.ShowAsync();
        }

        private void CoreWindow_CharacterReceived(CoreWindow sender,
                                                 CharacterReceivedEventArgs args)
        {
            // KeyCode 27 = Escape key, KeyCode 8 = Backspace
            if ((args.KeyCode != 27 || mediaPlayer.IsFullWindow) && args.KeyCode != 8)
            {
                // System.Diagnostics.Debug.WriteLine("Pressed: " + args.KeyCode);
                return;
            }

            // Detatch from key inputs event
            var coreWindow = Window.Current.CoreWindow;
            coreWindow.CharacterReceived -= CoreWindow_CharacterReceived;

            //Go back, close window, confirm, etc.
            On_BackRequested();
        }
        private void ExitPlayer(IUICommand command)
        {
            On_BackRequested();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            On_BackRequested();
        }

        private void SystemNavigationManager_BackRequested(
    object sender,
    BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = On_BackRequested();
            }
        }

        // Handles system-level BackRequested events and page-level back button Click events
        private bool On_BackRequested()
        {
            mediaPlayer.Source = null;
            AllowDisplayOff();
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                return true;
            }
            else
            {
                // one path up
                if (pathComponents.Count > 0)
                {
                    pathComponents.RemoveAt(pathComponents.Count - 1);
                }
                Frame.Navigate(typeof(MainPage), new VideoNavigationParameter(pathComponents, oneDriveClient));
                return true;
            }
        }

        private void BackInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            On_BackRequested();
            args.Handled = true;
        }
    }

}
