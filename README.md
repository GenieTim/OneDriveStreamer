# CloudStreamer

Stream Movies from OneDrive with this App for XBox (&amp; Windows)

- [CloudStreamer](#cloudstreamer)
  - [Why?](#why)
  - [How to Use](#how-to-use)
  - [What Works](#what-works)
  - [How to Build Yourself](#how-to-build-yourself)
  - [Contributions](#contributions)
  - [Disclaimer](#disclaimer)

## Why?

- The OneDrive App for XBox does not allow to access media controls (pause/play, time in movie, etc.)
- Edge stops playing movies after ca. 1:40. 
- No file manager on newer devices means, that downloading & delting after viewing is not easy/possible either

## How to Use

You can install it from the Microsoft store, [here](https://www.microsoft.com/en-us/p/onedrivestreamer/9ngfvc3zsf4k?activetab=pivot:overviewtab&cid=gihub).

[![English badge](https://developer.microsoft.com/store/badges/images/English_get-it-from-MS.png)](//www.microsoft.com/store/apps/9NGFVC3ZSF4K?cid=storebadge_github&ocid=badge)

## What Works

First, after installing the App, you log in with your Microsoft/OneDrive account.
Then, you get a list of your files. Feel free to click through until you find the movie you want to watch.
Click it and the movie will be played. 

## How to Build Yourself

The following NuGet Packages are required:

-  Microsoft.Extensions.Configuration.Json, 
-  Microsoft.Extensions.Options.ConfigurationExtensions, 
-  Microsoft.NETCore.UniversamWindowsPlatform, 
-  Microsoft.OneDriveSDK, 
-  Microsoft.OneDriveSDK.Authentication, 
-  MimeTypeMapOfficial, 
-  System.Collections

## Contributions

Yes, please! There are many improvement possibilities.
There are also a few requirements though:
- keep the app navigatable by keyboard (and, therefore, also by XBox controller)
- use only APIs and SDKs compatible with XBox devices (e.g. no UWP-incompatible .Net 5 WPF)
- stay pleasant  

## Disclaimer

OneDrive is a registered trademark by Microsoft. 
Xbox is a registered trademark of Microsoft. 
This product is not affiliated with or endorsed by Microsoft.
All trademarks are properties of their respective owners and are used here for information purposes only.

CloudStreamer is formerly known as OneDriveStreamer.
