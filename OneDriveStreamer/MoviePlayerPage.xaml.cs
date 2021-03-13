using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.OneDrive.Sdk;
using Microsoft.Services.Store.Engagement;
using MimeTypes;
using OneDriveStreamer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
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
        private readonly ResourceLoader loader;
        private DateTimeOffset lastRetry;

        public MoviePlayerPage()
        {
            InitializeComponent();
            dlLinkAge = DateTimeOffset.UtcNow;

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested +=
                SystemNavigationManager_BackRequested;
            //
            KeepDisplayOn();

            StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
            logger.Log("moviePlayerPage");

            // 
            var coreWindow = Window.Current.CoreWindow;
            coreWindow.CharacterReceived += CoreWindow_CharacterReceived;
            //
            loader = ResourceLoader.GetForCurrentView();

            //
            mediaPlayer.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            mediaPlayer.MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;

            //
            try
            {
                Analytics.TrackEvent("MoviePlayerPage");
            }
            catch (Exception e)
            {
                // let's not do anything more about this.
                System.Diagnostics.Debug.WriteLine("Exception when submitting analytics: " + e.Message);
            }
        }

        private async void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("MediaPlayer MediaFailed");
            // only retry if last retry was not within last 2 minutes
            if (DateTimeOffset.UtcNow.CompareTo(lastRetry.AddMinutes(2)) < 0)
            {
                // reload
                await SetVideoSourceKeepingPlaytime(false);
            }
            else
            {
                Crashes.TrackError(new Exception(args.ErrorMessage, args.ExtendedErrorCode), new Dictionary<string, string>
                    {
                        { "LastRetry", lastRetry.ToString() },
                        { "Now", DateTimeOffset.UtcNow.ToString() },
                        { "Where", "MoviePlayerPage.xaml:MediaPlayer_MediaFailed1"}
                    });
                try
                {
                    await ExitOrRetryWithMessage("MediaPlayer failed (" + args.Error.ToString() + "). Error message: " + args.ErrorMessage);
                }
                catch (Exception e)
                {
                    Crashes.TrackError(e, new Dictionary<string, string>
                    {
                        { "On", "Call Error Dialog" },
                        { "Where", "MoviePlayerPage.xaml:MediaPlayer_MediaFailed1"},
                        { "DueTo", args.ErrorMessage}
                    });
                }
            }
        }

        private async void PlaybackSession_PlaybackStateChangedAsync(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState.Equals(MediaPlaybackState.Paused))
            {
                AllowDisplayOff();
            }
            else
            {
                KeepDisplayOn();
            }

            // Problem: if we do this, the playback springs back after changing manually the playback position
            //if (sender.PlaybackState.Equals(MediaPlaybackState.Buffering))
            //{
            //    await UpdateMovieSourceIfNecessary();
            //}
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
                System.Diagnostics.Debug.WriteLine("Error requesting active display: ", e.Message);
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
                System.Diagnostics.Debug.WriteLine("Error requesting active display release: ", e.Message);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameters = (VideoNavigationParameter)e.Parameter;
            oneDriveClient = parameters.oneDriveClient;
            pathComponents = parameters.PathComponents;
            _ = SetVideoSourceKeepingPlaytime(true);
        }
        private void InitializeMovie(IUICommand c)
        {
            _ = SetVideoSourceKeepingPlaytime(false);
        }

        private async Task SetVideoSourceKeepingPlaytime(bool newMovie)
        {
            var videoPath = $"/{string.Join("/", pathComponents)}";
            dlLinkAge = DateTimeOffset.UtcNow;
            lastRetry = DateTimeOffset.UtcNow;
            try
            {
                TimeSpan currentPlaytime = new TimeSpan(0);
                if (!newMovie)
                {
                    try
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            currentPlaytime = mediaPlayer.MediaPlayer.PlaybackSession.Position;
                        });
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine("Error fetching current playtime: ", e.Message);
                    }
                }
                var builder = oneDriveClient.Drive.Root.ItemWithPath(videoPath);
                var file = await builder.Request().GetAsync();
                string mimeType = MimeTypeMap.GetMimeType(file.Name);
                System.Diagnostics.Debug.WriteLine("Playing item with mime type: " + mimeType);
                try
                {
                    Analytics.TrackEvent("VideoUri Set", new Dictionary<string, string> {
                     { "MimeType", mimeType }
                    });
                }
                catch (Exception e)
                {
                    // let's not do anything more about this.
                    System.Diagnostics.Debug.WriteLine("Exception when submitting analytics: " + e.Message);
                }

                if (file.AdditionalData.TryGetValue("@content.downloadUrl", out object downloadUrl))
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            mediaPlayer.Source = MediaSource.CreateFromUri(new Uri((string)downloadUrl));
                        });
                }
                else
                {
                    // NOTE: this stream can lead to out of memory exceptions.
                    // might want to try also the VLC movie element again?
                    Stream contentStream = await builder.Content.Request().GetAsync();
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        mediaPlayer.Source = MediaSource.CreateFromStream(contentStream.AsRandomAccessStream(), mimeType);
                    });
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    mediaPlayer.MediaPlayer.PlaybackSession.Position = currentPlaytime;

                    // setup listeners
                    if (newMovie)
                    {
                        mediaPlayer.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChangedAsync;
                    }
                });
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string>
                    {
                        { "On", "Setting Video Source" },
                        { "Where", "MoviePlayerPage.xaml:SetVideoSourceKeepingPlaytime"},
                        { "DueTo", ex.Message},
                        { "IsNewMovie", newMovie.ToString()}
                    });
                try
                {
                    await ExitOrRetryWithMessage("Failed to load movie. Error: " + ex.ToString() + " (" + ex.Message + ")");
                }
                catch (Exception e)
                {
                    Crashes.TrackError(e, new Dictionary<string, string>
                    {
                        { "On", "Call Error Dialog" },
                        { "Where", "MoviePlayerPage.xaml:SetVideoSourceKeepingPlaytime"},
                        { "DueTo", ex.Message}
                    });
                }
            }
        }

        private async Task ExitOrRetryWithMessage(string message)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    // Create the message dialog and set its content
                    var messageDialog = new MessageDialog(message);

                    // Add commands and set their callbacks; both buttons use the same callback function instead of inline event handlers
                    messageDialog.Commands.Add(new UICommand(loader.GetString("Error/TryAgain"), new UICommandInvokedHandler(InitializeMovie)));
                    messageDialog.Commands.Add(new UICommand(loader.GetString("Error/BackToMain"), new UICommandInvokedHandler(ExitPlayer)));

                    // Set the command that will be invoked by default
                    messageDialog.DefaultCommandIndex = 0;

                    // Set the command to be invoked when escape is pressed
                    messageDialog.CancelCommandIndex = 1;

                    // Show the message dialog
                    await messageDialog.ShowAsync();
                });
            }
            catch (Exception e)
            {
                Crashes.TrackError(e, new Dictionary<string, string>
                    {
                        { "On", "Show Message Dialog" },
                        { "Where", "MoviePlayerPage.xaml:ExitOrRetryWithMessage"}
                    });
            }
        }

        private void CoreWindow_CharacterReceived(CoreWindow sender,
                                                 CharacterReceivedEventArgs args)
        {
            // KeyCode 27 = Escape key, KeyCode 8 = Backspace
            if ((args.KeyCode != 27 || mediaPlayer.IsFullWindow) && args.KeyCode != 8)
            {
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

        private void SystemNavigationManager_BackRequested(object sender, BackRequestedEventArgs e)
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
