using Microsoft.OneDrive.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneDriveStreamer.Utils
{
    class VideoNavigationParameter
    {
        public VideoNavigationParameter() { }
        public VideoNavigationParameter(string url, string videoPath, OneDriveClient client)
        {
            this.videoUrl = url;
            this.videoPath = videoPath;
            this.oneDriveClient = client;
        }

        public string videoUrl { get; set; }
        public string videoPath { get; set; }
        public OneDriveClient oneDriveClient { get; set; }
    }
}
