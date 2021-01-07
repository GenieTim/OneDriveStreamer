using Microsoft.OneDrive.Sdk;
using System.Collections.Generic;

namespace OneDriveStreamer.Utils
{
    class VideoNavigationParameter
    {
        public VideoNavigationParameter() { }

        public VideoNavigationParameter(List<string> pathComponents, OneDriveClient oneDriveClient)
        {
            PathComponents = pathComponents;
            this.oneDriveClient = oneDriveClient;
        }

        public OneDriveClient oneDriveClient { get; set; }
        public List<string> PathComponents { get; }
    }
}
