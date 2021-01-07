using Microsoft.OneDrive.Sdk;
using Microsoft.OneDrive.Sdk.Authentication;
using OneDriveStreamer.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Security.Credentials;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

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
            backButton.Visibility = Visibility.Collapsed;

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested += SystemNavigationManager_BackRequested;
        }

        private async void ExitOrRetryWithMessage(string message)
        {
            // Create the message dialog and set its content
            var messageDialog = new MessageDialog(message);

            // Add commands and set their callbacks; both buttons use the same callback function instead of inline event handlers
            messageDialog.Commands.Add(new UICommand(
                "Try again",
                new UICommandInvokedHandler(this.ResetListFilesFolders)));
            messageDialog.Commands.Add(new UICommand(
                "Close app",
                new UICommandInvokedHandler(this.ExitApp)));

            // Set the command that will be invoked by default
            messageDialog.DefaultCommandIndex = 0;

            // Set the command to be invoked when escape is pressed
            messageDialog.CancelCommandIndex = 1;

            // Show the message dialog
            await messageDialog.ShowAsync();
        }
        private void ExitApp(IUICommand command)
        {
            CoreApplication.Exit();
        }

        private async void InitOneDrive(IUICommand command)
        {
            await this.InitOneDrive();
        }
        private async Task InitOneDrive()
        {
            string[] scopes = { "onedrive.readonly", "wl.signin" };
            try
            {
                // get provider
                var msaAuthProvider = new MsaAuthenticationProvider(
                    myClientId,
                    "https://login.live.com/oauth20_desktop.srf",
                    scopes,
                    new CredentialCache(),
                    new CredentialVault(myClientId));
                await msaAuthProvider.RestoreMostRecentFromCacheOrAuthenticateUserAsync();
                // init client
                this.oneDriveClient = new OneDriveClient("https://api.onedrive.com/v1.0", msaAuthProvider);
                var refreshtoken = (((MsaAuthenticationProvider)oneDriveClient.AuthenticationProvider).CurrentAccountSession).RefreshToken;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e, "error");
                this.ExitOrRetryWithMessage("Failed to authenticate with OneDrive. Error: " + e.ToString());
            }
        }

        private void SystemNavigationManager_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = this.On_BackRequested();
            }
        }

        private void ResetListFilesFolders(IUICommand command)
        {
            this.pathComponents.Clear();
            this.ListFilesFolders("/");
        }

        protected override void OnNavigatedTo(NavigationEventArgs eArgs)
        {
            if (pathComponents.Count > 0)
            {
                try
                {
                    var parameters = (VideoNavigationParameter)eArgs.Parameter;
                    if (parameters != null)
                    {
                        this.pathComponents = parameters.PathComponents;
                        this.oneDriveClient = parameters.oneDriveClient;
                        this.ListFilesFolders();
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e, "warning");

                }
                // e.g. from back button, need to remove file elements, possibly
                // this here is faster than loading the element to check, but worse UX
                if (pathComponents[pathComponents.Count - 1].Contains("."))
                {
                    // one path up
                    pathComponents.RemoveAt(pathComponents.Count - 1);
                }
                this.ListFilesFolders();
            }
        }

        private void ListFilesFolders()
        {
            string currentPath = "/";
            if (pathComponents.Count > 0)
            {
                currentPath += String.Join("/", pathComponents) + "/";
            }
            this.ListFilesFolders(currentPath);
        }

        private async void ListFilesFolders(string path)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (path == "/")
                {
                    pathText.Text = "OneDrive Streamer";
                }
                else
                {
                    pathText.Text = path;
                }
                fileScrollViewer.Visibility = Visibility.Collapsed;
                progressRing.Visibility = Visibility.Visible;
                progressRing.IsActive = true;
            });

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
                // TODO: list more upon scrolling
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e, "error");
                this.ExitOrRetryWithMessage("Failed to load Files from OneDrive. Error: " + e.ToString());
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
                 progressRing.IsActive = false;
                 progressRing.Visibility = Visibility.Collapsed;
                 fileScrollViewer.Visibility = Visibility.Visible;
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
            backButton.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("Clicked on " + dataitem.Name);
            pathComponents.Add(dataitem.Name);
            string currentPath = "/" + String.Join("/", pathComponents) + "/";
            //... here you can perform actions on the dataitem.
            if (dataitem.Folder != null)
            {
                this.ListFilesFolders(currentPath);
            }

            else if (dataitem.Video != null || dataitem.Audio != null)
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
                this.ListFilesFolders();
                if (pathComponents.Count > 0)
                {
                    backButton.Visibility = Visibility.Visible;

                }
                else
                {
                    backButton.Visibility = Visibility.Collapsed;
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
