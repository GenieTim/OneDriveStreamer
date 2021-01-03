using Microsoft.OneDrive.Sdk;
using Microsoft.OneDrive.Sdk.Authentication;
using OneDriveStreamer.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Security.Credentials;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OneDriveStreamer
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string myClientId;
        private List<string> pathComponents = new List<string>();
        // ideally, loading would be atomic. As there is only one UI thread, we make use of that
        // the idea is to prevent walking the path too far
        private bool loading = false;
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
            this.ListFilesFolders("/");
            filesListControl.ItemsSource = fileItems;
            backButtonGrid.Visibility = Visibility.Collapsed;

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested +=
                SystemNavigationManager_BackRequested;
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

        private void SystemNavigationManager_BackRequested(
    object sender,
    BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = this.On_BackRequested();
            }
        }


        private async void ListFilesFolders(string path)
        {
            if (this.oneDriveClient == null)
            {
                await this.InitOneDrive();
            }

            try
            {
                if (path == "/")
                {
                    this.files = (ItemChildrenCollectionPage)await oneDriveClient.Drive.Root.Children.Request().GetAsync();
                }
                else
                {
                    this.files = (ItemChildrenCollectionPage)await oneDriveClient.Drive.Root.ItemWithPath(path).Children.Request().GetAsync();
                }
            }
            catch (Exception e)
            {
                // TODO: show the message to the user
                System.Diagnostics.Debug.WriteLine(e, "error");
                return;
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
                 this.loading = false;
             });

            System.Diagnostics.Debug.WriteLine("Got " + fileItems.Count() + " files");
        }

        private void filesListControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            // TODO: find a better way to prevent race conditions
            if (!this.loading)
            {
                this.loading = true;
                var dataitem = e.ClickedItem as Item;
                this.ItemClicked(dataitem);
            }
        }

        private void ItemClicked(Item dataitem)
        {
            backButtonGrid.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("Clicked on " + dataitem.Name);
            pathComponents.Add(dataitem.Name);
            string currentPath = "/" + String.Join("/", pathComponents) + "/";
            //... here you can perform actions on the dataitem.
            if (dataitem.Folder != null)
            {
                this.ListFilesFolders(currentPath);
            }

            else if (dataitem.Video != null)
            {
                Frame.Navigate(typeof(MoviePlayerPage), new VideoNavigationParameter(pathComponents, oneDriveClient));
            }
            // TODO: do something else!
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            On_BackRequested();
        }

        // Handles system-level BackRequested events and page-level back button Click events
        private bool On_BackRequested()
        {
            this.loading = true;
            if (this.pathComponents.Count != 0)
            {
                // one path up
                pathComponents.RemoveAt(pathComponents.Count - 1);
                string currentPath = "/" + String.Join("/", pathComponents) + "/";
                this.ListFilesFolders(currentPath);
                if (pathComponents.Count > 0)
                {
                    backButtonGrid.Visibility = Visibility.Visible;

                }
                else
                {
                    backButtonGrid.Visibility = Visibility.Collapsed;
                }
                return true;
            }
            return false;
        }

        private void BackInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            On_BackRequested();
            args.Handled = true;
        }
    }
}
