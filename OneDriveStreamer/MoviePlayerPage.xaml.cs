using OneDriveStreamer.Utils;
using System.IO;
using Windows.Media.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using MimeTypes;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Core;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace OneDriveStreamer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MoviePlayerPage : Page
    {
        public MoviePlayerPage()
        {
            this.InitializeComponent();

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested +=
                SystemNavigationManager_BackRequested;
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameters = (VideoNavigationParameter)e.Parameter;
            var client = parameters.oneDriveClient;
            var pathComp = parameters.PathComponents;
            var videoPath = "/" + string.Join("/", pathComp);
            var builder = client.Drive.Root.ItemWithPath(videoPath);
            var file = await builder.Request().GetAsync();
            Stream contentStream = await builder.Content.Request().GetAsync();
            string mimeType = MimeTypeMap.GetMimeType(file.Name);
            mediaPlayer.Source = MediaSource.CreateFromStream(contentStream.AsRandomAccessStream(), mimeType);
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
            {
                this.Frame.Navigate(typeof(MoviePlayerPage));
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
