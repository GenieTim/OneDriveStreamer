# CloudStreamer

Stream Movies from OneDrive with this App for XBox (&amp; Windows)

- [CloudStreamer](#cloudstreamer)
  - [Why?](#why)
  - [How to Use](#how-to-use)
  - [How to build yourself](#how-to-build-yourself)
  - [Current State](#current-state)
  - [Disclaimer](#disclaimer)

## Why?

- The OneDrive App for XBox does not allow to access media controls (pause/play, time in movie, etc.)
- Edge stops playing movies after ca. 1:40. 
- No file manager on newer devices means, that downloading & delting after viewing is not easy/possible either

## How to Use

Currently, this is not a polished product (but published in the Microsoft store, [here](https://www.microsoft.com/en-us/p/onedrivestreamer/9ngfvc3zsf4k?activetab=pivot:overviewtab)).

## How to build yourself

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

## Current State

To be updated.
At the time of writing, it runs, but UX & UI are not ideal.

## Disclaimer

OneDrive is a registered trademark by Microsoft. 
Xbox is a registered trademark of Microsoft. 
This product is not affiliated with or endorsed by Microsoft.
All trademarks are properties of their respective owners and are used here for information purposes only.

CloudStreamer is formerly known as OneDriveStreamer.
