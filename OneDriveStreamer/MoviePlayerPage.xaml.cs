using OneDriveStreamer.Utils;
using System.IO;
using Windows.Media.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using MimeTypes;

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
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameters = (VideoNavigationParameter)e.Parameter;
            var client = parameters.oneDriveClient;
            var builder = client.Drive.Root.ItemWithPath(parameters.videoPath);
            var file = await builder.Request().GetAsync();
            Stream contentStream = await builder.Content.Request().GetAsync();
            string mimeType = MimeTypeMap.GetMimeType(file.Name);
            mediaPlayer.Source = MediaSource.CreateFromStream(contentStream.AsRandomAccessStream(), mimeType);
            // parameters.Name
            // parameters.Text
            // ...
        }
    }

}
