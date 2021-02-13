using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.OneDrive.Sdk;
using Microsoft.Services.Store.Engagement;
using OneDriveStreamer.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
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
        private const string BaseUrl = "https://api.onedrive.com/v1.0";
        private List<string> pathComponents = new List<string>();
        // ideally, loading would be atomic. As there is only one UI thread, we make use of that
        // the idea is to prevent walking the path too far
        private bool loading = false;
        private OneDriveClient oneDriveClient = null;
        private OnlineIdAuthenticationProvider authenticationProvider;
        private ItemChildrenCollectionPage files = null;
        // The "collection" that is shown in the UI
        private IncrementalLoadingCollection fileItems = null;
        private readonly string[] sortOrders = { "name", "lastModifiedDateTime", "size" };
        private string currentSortBy = "name";
        private string currentSortByDir = "asc";
        private ResourceLoader l18nLoader;

        //
        private static List<MessageDialog> DialogQueue { get; } = new List<MessageDialog>();

        public MainPage()
        {
            InitializeComponent();
            backButton.Visibility = Visibility.Collapsed;
            fileScrollViewer.ItemsSource = fileItems;

            try
            {
                // add localized options
                l18nLoader = ResourceLoader.GetForCurrentView();
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);
                System.Diagnostics.Debug.WriteLine(e, "error");
            }
            if (l18nLoader != null)
            {
                // ...  to sort by
                sortComboBox.Items.Add(l18nLoader.GetString("Sort/Name"));
                sortComboBox.Items.Add(l18nLoader.GetString("Sort/Size"));
                sortComboBox.Items.Add(l18nLoader.GetString("Sort/LastModified"));
                sortComboBox.SelectedIndex = 0;
                // ... to sort direction
                sortDirComboBox.Items.Add(l18nLoader.GetString("Sort/Ascending"));
                sortDirComboBox.Items.Add(l18nLoader.GetString("Sort/Descending"));
                sortDirComboBox.SelectedIndex = 0;
            }

            // loading data: not necessary as is done in OnNavigatedTo

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested += SystemNavigationManager_BackRequestedAsync;

            StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
            logger.Log("mainPage");
            try
            {
                Analytics.TrackEvent("MainPage Loaded");
            }
            catch (Exception e)
            {
                // let's not do anything more about this.
                System.Diagnostics.Debug.WriteLine("Exception when submitting analytics: " + e.Message);
            }
        }

        private async Task ExitOrRetryWithMessage(string message)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    { // Create the message dialog and set its content
                        var messageDialog = new MessageDialog(message, l18nLoader.GetString("Error/Title"));

                        // Add commands and set their callbacks; both buttons use the same callback function instead of inline event handlers
                        messageDialog.Commands.Add(new UICommand(
                           l18nLoader.GetString("Error/TryAgain"),
                            new UICommandInvokedHandler(async (IUICommand command) => { await ResetListFilesFolders(); await Dialog_Closed(messageDialog); })));
                        messageDialog.Commands.Add(new UICommand(
                           l18nLoader.GetString("Error/CloseApp"),
                            new UICommandInvokedHandler(ExitApp)));

                        // Set the command that will be invoked by default
                        messageDialog.DefaultCommandIndex = 0;

                        // Set the command to be invoked when escape is pressed
                        messageDialog.CancelCommandIndex = 1;

                        //
                        DialogQueue.Add(messageDialog);

                        // Show the message dialog if only one in queue
                        if (DialogQueue.Count == 1)
                        {
                            await messageDialog.ShowAsync();
                        }
                    }
                );
                try
                {
                    Analytics.TrackEvent("Showing ErrorDialog", new Dictionary<string, string> {
                     { "Message", message }
                    });
                }
                catch (Exception e)
                {
                    // let's not do anything about this.
                    System.Diagnostics.Debug.WriteLine("Exception when submitting analytics: " + e.Message);
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(e, new Dictionary<string, string>
                    {
                        { "On", "Show Message Dialog" },
                        { "Where", "MainPage.xaml:ExitOrRetryWithMessage"}
                    });
            }
        }

        private static async Task Dialog_Closed(MessageDialog sender)
        {
            try
            {
                DialogQueue.Remove(sender);
                if (DialogQueue.Count > 0)
                {
                    await DialogQueue[0].ShowAsync();
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(e, new Dictionary<string, string>
                    {
                        { "On", "Show Message Dialog" },
                        { "Where", "MainPage.xaml:Dialog_Closed"}
                    });
            }
        }

        private void ExitApp(IUICommand command)
        {
            CoreApplication.Exit();
        }

        private async Task InitOneDrive()
        {
            string[] scopes = { "onedrive.readonly", "wl.signin" };
            try
            {
                // get provider
                authenticationProvider = new OnlineIdAuthenticationProvider(scopes);
                await authenticationProvider.RestoreMostRecentFromCacheOrAuthenticateUserAsync();
                // init client
                oneDriveClient = new OneDriveClient(BaseUrl, authenticationProvider);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e, "error");
                try
                {
                    await ExitOrRetryWithMessage("Failed to authenticate with OneDrive. Error: " + e.ToString());
                }
                catch (Exception ex)
                {
                    Crashes.TrackError(ex, new Dictionary<string, string>
                    {
                        { "On", "Call Error Dialog" },
                        { "Where", "MainPage.xaml:InitOneDrive"},
                        { "DueTo", e.Message}
                    });
                }
            }
        }

        private async Task UpdateOrInitOneDriveAuthIfNecessary()
        {
            if (oneDriveClient == null)
            {
                await InitOneDrive();
            }

            if (oneDriveClient == null)
            {
                return;
            }

            if (authenticationProvider != null && authenticationProvider.CurrentAccountSession.IsExpiring)
            {
                await authenticationProvider.RestoreMostRecentFromCacheOrAuthenticateUserAsync();
                oneDriveClient.AuthenticationProvider = authenticationProvider;
            }
        }

        private void SystemNavigationManager_BackRequestedAsync(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = pathComponents.Count != 0;
            }
            _ = On_BackRequestedAsync();
        }

        private async Task ResetListFilesFolders()
        {
            pathComponents.Clear();
            await ListFilesFolders("/");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnNavigatedTo.");
            if (e.Parameter is VideoNavigationParameter parameters)
            {
                oneDriveClient = parameters.oneDriveClient;
                pathComponents = parameters.PathComponents;
            }
            else if (pathComponents.Count > 0 && pathComponents[pathComponents.Count - 1].Contains("."))
            {
                // one path up
                pathComponents.RemoveAt(pathComponents.Count - 1);
            }
            // load data
            try
            {
                _ = ListFilesFolders();
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
                try
                {
                    _ = ExitOrRetryWithMessage("Failed to list files and folders: " + ex.Message);
                }
                catch (Exception exc)
                {
                    Crashes.TrackError(exc, new Dictionary<string, string>
                    {
                        { "On", "Call Error Dialog" },
                        { "Where", "MainPage.xaml:OnNavigatedTo"},
                        { "DueTo", ex.Message}
                    });
                }
            }
        }

        private async Task ListFilesFolders()
        {
            string currentPath = "/";
            if (pathComponents.Count > 0)
            {
                currentPath += String.Join("/", pathComponents) + "/";
            }
            await ListFilesFolders(currentPath);
        }

        private async Task ListFilesFolders(string path)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // update the title path
                if (path == "/")
                {
                    pathText.Text = "CloudStreamer";
                }
                else
                {
                    pathText.Text = path;
                }
                fileScrollViewer.Visibility = Visibility.Collapsed;
                progressRing.Visibility = Visibility.Visible;
                progressRing.IsActive = true;
            });

            // handle login
            await UpdateOrInitOneDriveAuthIfNecessary();

            if (oneDriveClient == null)
            {
                try
                {
                    await ExitOrRetryWithMessage("Failed to authenticate with OneDrive.");
                }
                catch (Exception ex)
                {
                    Crashes.TrackError(ex, new Dictionary<string, string>
                    {
                        { "On", "Call Error Dialog" },
                        { "Where", "MainPage.xaml:ListFilesFolders"},
                        { "DueTo", "oneDriveClient null"}
                    });
                }
                return;
            }

            try
            {
                IItemChildrenCollectionRequest request;
                if (path == "/")
                {
                    request = oneDriveClient.Drive.Root.Children.Request();
                }
                else
                {
                    request = oneDriveClient.Drive.Root.ItemWithPath(path).Children.Request();
                }
                IItemChildrenCollectionRequest sortedRequest = request.OrderBy(currentSortBy + " " + currentSortByDir);
                sortedRequest = sortedRequest.Expand("thumbnails");
                files = (ItemChildrenCollectionPage)await sortedRequest.GetAsync();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e, "error");
                try
                {
                    await ExitOrRetryWithMessage("Failed to load Files from OneDrive. Error: " + e.ToString());
                }
                catch (Exception ex)
                {
                    Crashes.TrackError(ex, new Dictionary<string, string>
                    {
                        { "On", "Call Error Dialog" },
                        { "Where", "MainPage.xaml:ListFilesFolders"},
                        { "DueTo", e.Message}
                    });
                }
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 fileItems = new IncrementalLoadingCollection(files);
                 fileScrollViewer.ItemsSource = fileItems;
                 loading = false;
                 progressRing.IsActive = false;
                 progressRing.Visibility = Visibility.Collapsed;
                 fileScrollViewer.Visibility = Visibility.Visible;
             });
            System.Diagnostics.Debug.WriteLine("Got " + fileItems.Count() + " files");
        }

        private void FilesListControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            // NOTE: might want to find a better way to prevent race conditions
            if (!loading)
            {
                loading = true;
                var dataitem = e.ClickedItem as Item;
                _ = ItemClicked(dataitem);
            }
        }

        private async Task ItemClicked(Item dataitem)
        {
            backButton.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("Clicked on " + dataitem.Name);
            pathComponents.Add(dataitem.Name);
            string currentPath = $"/{String.Join("/", pathComponents)}/";
            //... here you can perform actions on the dataitem.
            if (dataitem.Folder != null)
            {
                await ListFilesFolders(currentPath);
            }
            else if (dataitem.Video != null || dataitem.Audio != null)
            {
                Frame.Navigate(typeof(MoviePlayerPage), new VideoNavigationParameter(pathComponents, oneDriveClient));
            }
            else
            {
                // NOTE: might want to open file in external app?
                pathComponents.RemoveAt(pathComponents.Count - 1);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _ = On_BackRequestedAsync();
        }

        // Handles system-level BackRequested events and page-level back button Click events
        private async Task<bool> On_BackRequestedAsync()
        {
            loading = true;
            System.Diagnostics.Debug.WriteLine("On_BackRequested.");
            if (pathComponents.Count != 0)
            {
                // one path up
                pathComponents.RemoveAt(pathComponents.Count - 1);
                await ListFilesFolders();
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
            _ = On_BackRequestedAsync();
            args.Handled = true;
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (oneDriveClient == null)
            {
                // don't handle change on first app load
                return;
            }
            System.Diagnostics.Debug.WriteLine("SortComboBox_SelectionChanged.");
            var box = sender as ComboBox;
            if (box.SelectedValue != null)
            {
                currentSortBy = sortOrders[box.SelectedIndex];
                if (currentSortBy == null)
                {
                    currentSortBy = "name";
                }
            }
            else
            {
                currentSortBy = "name";
            }
            _ = ListFilesFolders();
        }
        private void SortDirComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (oneDriveClient == null)
            {
                // don't handle change on first app load
                return;
            }
            System.Diagnostics.Debug.WriteLine("SortDirComboBox_SelectionChanged.");
            var box = sender as ComboBox;
            if (box.SelectedValue != null)
            {
                currentSortByDir = box.SelectedIndex == 0 ? "asc" : "desc";
            }
            else
            {
                currentSortByDir = "name";
            }
            _ = ListFilesFolders();
        }

        public class IncrementalLoadingCollection : ObservableCollection<Item>, ISupportIncrementalLoading
        {
            private ItemChildrenCollectionPage lastFiles;

            public IncrementalLoadingCollection(ItemChildrenCollectionPage files)
            {
                if (files != null)
                {
                    foreach (Item i in files)
                    {
                        Add(i);

                    }
                }
                lastFiles = files;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async cancelToken =>
                {
                    if (lastFiles == null)
                    {
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                    try
                    {
                        var nextPage = lastFiles.NextPageRequest;
                        if (nextPage != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Listing additional files.");
                            ItemChildrenCollectionPage newFiles = (ItemChildrenCollectionPage)await nextPage.GetAsync();
                            foreach (Item i in newFiles)
                            {
                                Add(i);
                                System.Diagnostics.Debug.WriteLine("Adding file " + i.Name + " for more files.");
                            }
                            // NOTE: currently, there is no break - it just keeps fetching new items
                            System.Diagnostics.Debug.WriteLine("Got " + newFiles.Count() + " additional files");
                            lastFiles = newFiles;
                            return new LoadMoreItemsResult { Count = Convert.ToUInt32(newFiles.Count()) };
                        }
                        else
                        {
                            return new LoadMoreItemsResult { Count = 0 };
                        }
                    }
                    catch (Exception e)
                    {
                        Crashes.TrackError(e, new Dictionary<string, string>
                    {
                        { "On", "List Additional Files" },
                        { "Where", "MainPage.xaml:LoadMoreItemsAsync"},
                        { "DueTo", e.Message}
                    });
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                });
            }

            bool ISupportIncrementalLoading.HasMoreItems => lastFiles == null || lastFiles.NextPageRequest != null;
        }
    }
}
