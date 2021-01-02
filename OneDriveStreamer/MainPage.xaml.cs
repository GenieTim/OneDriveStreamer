using Microsoft.OneDrive.Sdk;
using Microsoft.OneDrive.Sdk.Authentication;
using OneDriveStreamer.Utils;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Security.Credentials;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OneDriveStreamer
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string myClientId;
        private string currentPath = "/";
        private OneDriveClient oneDriveClient;
        private ItemChildrenCollectionPage files;
        private ObservableCollection<Item> fileItems = new ObservableCollection<Item>();
        // The "collection" that is shown in the UI
        public ObservableCollection<string> Items = new ObservableCollection<string>();

        public MainPage()
        {
            this.InitializeComponent();
            var config = new AppConfig();
            this.myClientId = config.GetClientID();
            this.ListFilesFolders(currentPath);
            filesListControl.ItemsSource = fileItems;
        }

        private async Task InitOneDrive()
        {
            string[] scopes = { "onedrive.readonly", "wl.signin" };
            // get provider
            var msaAuthProvider = new MsaAuthenticationProvider(
                myClientId,
                "https://login.live.com/oauth20_desktop.srf",
                scopes,
                null,
                new CredentialVault(myClientId));
            await msaAuthProvider.RestoreMostRecentFromCacheOrAuthenticateUserAsync();
            // init client
            this.oneDriveClient = new OneDriveClient("https://api.onedrive.com/v1.0", msaAuthProvider);
            var refreshtoken = (((MsaAuthenticationProvider)oneDriveClient.AuthenticationProvider).CurrentAccountSession).RefreshToken;
        }

        private async void ListFilesFolders(string path)
        {
            if (this.oneDriveClient == null)
            {
                await this.InitOneDrive();
            }

            if (path == "/")
            {
                this.files = (ItemChildrenCollectionPage)await oneDriveClient.Drive.Root.Children.Request().GetAsync();
            }
            else
            {
                this.files = (ItemChildrenCollectionPage)await oneDriveClient.Drive.Root.ItemWithPath(path).Children.Request().GetAsync();
            }
            var list = files.ToList();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
             {
                 fileItems.Clear();
                 foreach (Item i in list)
                 {
                     fileItems.Add(i);
                     System.Diagnostics.Debug.WriteLine("Adding file " + i.Name);
                 }
             });

            System.Diagnostics.Debug.WriteLine("Got " + fileItems.Count() + " files");
        }

        private void File_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var dataitem = button.DataContext as Item;
            System.Diagnostics.Debug.WriteLine("Clicked on " + dataitem.Name);
            //... here you can perform actions on the dataitem.
            if (dataitem.Folder != null)
            {
                currentPath += dataitem.Name + "/";
                this.ListFilesFolders(currentPath);
            }

            else if (dataitem.Video != null)
            {
                Frame.Navigate(typeof(MoviePlayerPage), new VideoNavigationParameter(dataitem.WebUrl, currentPath + dataitem.Name, oneDriveClient));
            }
        }
    }
}
