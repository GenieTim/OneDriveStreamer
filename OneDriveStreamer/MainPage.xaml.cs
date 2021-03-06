﻿using Microsoft.OneDrive.Sdk;
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
        private List<string> pathComponents = new List<string>();
        // ideally, loading would be atomic. As there is only one UI thread, we make use of that
        // the idea is to prevent walking the path too far
        private bool loading = false;
        private OneDriveClient oneDriveClient;
        private OnlineIdAuthenticationProvider authenticationProvider;
        private ItemChildrenCollectionPage files;
        // The "collection" that is shown in the UI
        private IncrementalLoadingCollection<Item> fileItems;
        private string[] sortOrders = { "name", "lastModifiedDateTime", "size" };
        private string currentSortBy = "name";

        public string currentSortByDir = "asc";
        private ResourceLoader l18nLoader;

        public MainPage()
        {
            InitializeComponent();
            backButton.Visibility = Visibility.Collapsed;
            try
            {
                ListFilesFolders("/");
            }
            catch (Exception e)
            {
                ExitOrRetryWithMessage("Failed to list files and folders: " + e.Message);
            }
            fileScrollViewer.ItemsSource = fileItems;

            try
            {
                // add localized options
                l18nLoader = ResourceLoader.GetForCurrentView();
            }
            catch (Exception e)
            {
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

            // Handling Page Back navigation behaviors
            SystemNavigationManager.GetForCurrentView().BackRequested += SystemNavigationManager_BackRequested;

            StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
            logger.Log("mainPage");
        }

        private async void ExitOrRetryWithMessage(string message)
        {
            // Create the message dialog and set its content
            var messageDialog = new MessageDialog(message);

            // Add commands and set their callbacks; both buttons use the same callback function instead of inline event handlers
            messageDialog.Commands.Add(new UICommand(
               l18nLoader.GetString("Error/TryAgain"),
                new UICommandInvokedHandler(ResetListFilesFolders)));
            messageDialog.Commands.Add(new UICommand(
               l18nLoader.GetString("Error/CloseApp"),
                new UICommandInvokedHandler(ExitApp)));

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
            await InitOneDrive();
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
                oneDriveClient = new OneDriveClient("https://api.onedrive.com/v1.0", authenticationProvider);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e, "error");
                ExitOrRetryWithMessage("Failed to authenticate with OneDrive. Error: " + e.ToString());
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
                ExitOrRetryWithMessage("Failed to authenticate with OneDrive.");
                return;
            }

            if (authenticationProvider != null && authenticationProvider.CurrentAccountSession.IsExpiring)
            {
                await authenticationProvider.RestoreMostRecentFromCacheOrAuthenticateUserAsync();
                oneDriveClient.AuthenticationProvider = authenticationProvider;
            }
        }

        private void SystemNavigationManager_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = On_BackRequested();
            }
        }

        private void ResetListFilesFolders(IUICommand command)
        {
            pathComponents.Clear();
            ListFilesFolders("/");
        }

        protected override void OnNavigatedTo(NavigationEventArgs eArgs)
        {
            var parameters = eArgs.Parameter as VideoNavigationParameter;
            if (parameters != null)
            {
                oneDriveClient = parameters.oneDriveClient;
                pathComponents = parameters.PathComponents;
            }
            else if (pathComponents.Count > 0)
            {
                // e.g. from back button, need to remove file elements, possibly
                // this here is faster than loading the element to check, but worse UX
                if (pathComponents[pathComponents.Count - 1].Contains("."))
                {
                    // one path up
                    pathComponents.RemoveAt(pathComponents.Count - 1);
                }
            }
            ListFilesFolders();
        }

        private void ListFilesFolders()
        {
            string currentPath = "/";
            if (pathComponents.Count > 0)
            {
                currentPath += String.Join("/", pathComponents) + "/";
            }
            ListFilesFolders(currentPath);
        }

        private async void ListFilesFolders(string path)
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
                ExitOrRetryWithMessage("Failed to authenticate with OneDrive.");
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
                //sortedRequest.
                files = (ItemChildrenCollectionPage)await sortedRequest.GetAsync();
                // TODO: list more upon scrolling
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e, "error");
                ExitOrRetryWithMessage("Failed to load Files from OneDrive. Error: " + e.ToString());
            }
            var list = files.ToList();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
             {
                 fileItems = new IncrementalLoadingCollection<Item>(this, files);
                 fileScrollViewer.ItemsSource = fileItems;
                 loading = false;
                 progressRing.IsActive = false;
                 progressRing.Visibility = Visibility.Collapsed;
                 fileScrollViewer.Visibility = Visibility.Visible;
             });
            System.Diagnostics.Debug.WriteLine("Got " + fileItems.Count() + " files");
        }

        private void filesListControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            // TODO: find a better way to prevent race conditions
            if (!loading)
            {
                loading = true;
                var dataitem = e.ClickedItem as Item;
                ItemClicked(dataitem);
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
                ListFilesFolders(currentPath);
            }
            else if (dataitem.Video != null || dataitem.Audio != null)
            {
                Frame.Navigate(typeof(MoviePlayerPage), new VideoNavigationParameter(pathComponents, oneDriveClient));
            }
            else
            {
                // TODO: open in external?
                pathComponents.RemoveAt(pathComponents.Count - 1);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            On_BackRequested();
        }

        // Handles system-level BackRequested events and page-level back button Click events
        private bool On_BackRequested()
        {
            loading = true;
            if (pathComponents.Count != 0)
            {
                // one path up
                pathComponents.RemoveAt(pathComponents.Count - 1);
                ListFilesFolders();
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

        private void sortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (oneDriveClient == null)
            {
                // don't handle change on first app load
                return;
            }
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
            ListFilesFolders();
        }
        private void sortDirComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (oneDriveClient == null)
            {
                // don't handle change on first app load
                return;
            }
            var box = sender as ComboBox;
            if (box.SelectedValue != null)
            {
                currentSortByDir = box.SelectedIndex == 0 ? "asc" : "desc";
            }
            else
            {
                currentSortByDir = "name";
            }
            ListFilesFolders();
        }

        public class IncrementalLoadingCollection<T> : ObservableCollection<Item>, ISupportIncrementalLoading
        {
            private MainPage parent;
            private ItemChildrenCollectionPage lastFiles;

            public IncrementalLoadingCollection(MainPage parent, ItemChildrenCollectionPage files)
            {
                foreach (Item i in files)
                {
                    Add(i);
                }
                lastFiles = files;
                this.parent = parent;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async cancelToken =>
                {
                    try
                    {
                        var nextPage = lastFiles.NextPageRequest;
                        if (nextPage != null)
                        {
                            ItemChildrenCollectionPage newFiles = (ItemChildrenCollectionPage)await nextPage.GetAsync();
                            foreach (Item i in newFiles)
                            {
                                Add(i);
                                System.Diagnostics.Debug.WriteLine("Adding file " + i.Name + " for more files.");
                            }
                            // TODO: currently, there is no break - it just keeps fetching new items
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
                        System.Diagnostics.Debug.WriteLine("Failed to list additional files.", e);
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                });
            }

            bool ISupportIncrementalLoading.HasMoreItems => lastFiles == null || lastFiles.NextPageRequest != null;
        }
    }
}
