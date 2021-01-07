using Microsoft.OneDrive.Sdk;
using MimeTypes;
using OneDriveStreamer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Media.Core;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace OneDriveStreamer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MoviePlayerPage : Page
    {
        private List<string> pathComponents = new List<string>();
        private OneDriveClient oneDriveClient;

        public MoviePlayerPage()
        {
            this.InitializeComponent();

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested +=
                SystemNavigationManager_BackRequested;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameters = (VideoNavigationParameter)e.Parameter;
            this.oneDriveClient = parameters.oneDriveClient;
            this.pathComponents = parameters.PathComponents;
            mediaPlayer.Visibility = Visibility.Collapsed;
            this.initializeMovie();
        }
        private void initializeMovie(IUICommand c)
        {
            this.initializeMovie();
        }

        private async void initializeMovie()
        {
            var videoPath = "/" + string.Join("/", this.pathComponents);
            try
            {
                mediaPlayer.Visibility = Visibility.Collapsed;
                mediaPlayerVlc.Visibility = Visibility.Visible;
                progress.Visibility = Visibility.Visible;
                var builder = oneDriveClient.Drive.Root.ItemWithPath(videoPath);
                var file = await builder.Request().GetAsync();
                string mimeType = MimeTypeMap.GetMimeType(file.Name);
                System.Diagnostics.Debug.WriteLine("Playing item with mine type: " + mimeType);
                // first, try with VLC
                object downloadUrl;
                if (file.AdditionalData.TryGetValue("@content.downloadUrl", out downloadUrl))
                {
                    var options = new Dictionary<string, object>();
                    mediaPlayerVlc.Options = options;
                    mediaPlayerVlc.Source = (string)downloadUrl;
                }
                // if we cannot, try with UWP MediaPlayer & Stream
                else
                {
                    mediaPlayerVlc.Visibility = Visibility.Collapsed;
                    mediaPlayer.Visibility = Visibility.Visible;
                    // TODO: this stream can lead to out of memory exceptions.
                    Stream contentStream = await builder.Content.Request().GetAsync();
                    mediaPlayer.Source = MediaSource.CreateFromStream(contentStream.AsRandomAccessStream(), mimeType);
                }
                progress.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                this.ExitOrRetryWithMessage("Failed to load movie. Error: " + ex.ToString());
            }
        }

        private async void ExitOrRetryWithMessage(string message)
        {
            // Create the message dialog and set its content
            var messageDialog = new MessageDialog(message);

            // Add commands and set their callbacks; both buttons use the same callback function instead of inline event handlers
            messageDialog.Commands.Add(new UICommand(
                "Try again",
                new UICommandInvokedHandler(this.initializeMovie)));
            messageDialog.Commands.Add(new UICommand(
                "Go Back to List",
                new UICommandInvokedHandler(this.ExitPlayer)));

            // Set the command that will be invoked by default
            messageDialog.DefaultCommandIndex = 0;

            // Set the command to be invoked when escape is pressed
            messageDialog.CancelCommandIndex = 1;

            // Show the message dialog
            await messageDialog.ShowAsync();
        }
        private void ExitPlayer(IUICommand command)
        {
            this.On_BackRequested();
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
                e.Handled = this.On_BackRequested();
            }
        }

        // Handles system-level BackRequested events and page-level back button Click events
        private bool On_BackRequested()
        {
            mediaPlayer.Source = null;
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
                return true;
            }
            else
            {   // one path up
                pathComponents.RemoveAt(pathComponents.Count - 1);
                this.Frame.Navigate(typeof(MoviePlayerPage), new VideoNavigationParameter(pathComponents, oneDriveClient));
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
