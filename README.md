# OneDriveStreamer
Stream Movies from OneDrive with this App for XBox (&amp; Windows)

## Why?

- The OneDrive App for XBox does not allow to access media controls (pause/play, time in movie, etc.)
- Edge stops playing movies after ca. 1:40. 
- No file manager on newer devices means, that downloading & delting after viewing is not easy/possible either

## How to Use

Currently, this is not a perfectly polished product
It runs, and you can install it from the Microsoft store, [here](https://www.microsoft.com/en-us/p/onedrivestreamer/9ngfvc3zsf4k?activetab=pivot:overviewtab).

[![English badge](https://developer.microsoft.com/store/badges/images/English_get-it-from-MS.png)](//www.microsoft.com/store/apps/9NGFVC3ZSF4K?cid=storebadge&ocid=badge)

## How it Works

First, after installing the App, you log in with your Microsoft/OneDrive account.
Then, you get a list of your files. Feel free to click through until you find the movie you want to watch.
Click it and the movie will be played. 

## How to Build Yourself

To run/test/contribute, you need to create a file `OneDriveStreamer/appsettings.json` with a 
content of:

```json
{
  "clientId": "some-string-you-need-to-get-yourself"
}
```

The following NuGet Packages are required:

-  Microsoft.Extensions.Configuration.Json, 
-  Microsoft.Extensions.Options.ConfigurationExtensions, 
-  Microsoft.NETCore.UniversamWindowsPlatform, 
-  Microsoft.OneDriveSDK, 
-  Microsoft.OneDriveSDK.Authentication, 
-  MimeTypeMapOfficial, 
-  System.Collections
-  Vlc.MediaElement

## Contributions

Yes, please! There are many improvement possibilities.
There are also a few requirements though:

- keep the app navigatable by keyboard (and, therefore, also by XBox controller)
- use only APIs and SDKs compatible with XBox devices (e.g. no UWP-incompatible .Net 5 WPF)
- stay pleasant  
