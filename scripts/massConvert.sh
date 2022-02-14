#!/bin/bash


# Skip AppKit ObjCRuntime
#  "ARKit" "AVFoundation" "AVKit" "Accelerate" "Accessibility" "Accounts" "AdSupport" "AddressBook" "AddressBookUI" "AppKit" "AssetsLibrary" "AudioToolbox" "AudioUnit" "AuthenticationServices" "BackgroundTasks" "BusinessChat" "CFNetwork" "CallKit" "CarPlay" "Carbon" "Chip" "ClassKit" "ClockKit" "CloudKit" "Compression" "Contacts" "CoreAnimation" "CoreBluetooth" "CoreData" "CoreFoundation" "CoreGraphics" "CoreHaptics" "CoreImage" "CoreLocation" "CoreML" "CoreMedia" "CoreMidi" "CoreMotion" "CoreServices" "CoreSpotlight" "CoreTelephony" "CoreText" "CoreVideo" "CoreWlan" "Darwin" "EventKit" "EventKitUI" "ExternalAccessory" "FileProvider" "FinderSync" "Foundation" "GLKit" "GameController" "GameKit" "GameplayKit" "HealthKit" "HomeKit" "IOSurface" "ImageCaptureCore" "ImageIO" "ImageKit" "InputMethodKit" "Intents" "JavaScriptCore" "LocalAuthentication" "MLCompute" "MapKit" "MediaAccessibility" "MediaLibrary" "MediaPlayer" "MediaToolbox" "MessageUI"
# "Metal" "MetalKit" "MetalPerformanceShaders" "MetricKit" "MobileCoreServices" "ModelIO" "MultipeerConnectivity" "NativeTypes" "NaturalLanguage" "NearbyInteraction" "Network" "NetworkExtension" "NewsstandKit" "NotificationCenter"  
# "OpenGL" "OpenGLES" "PassKit" "PdfKit" "Photos" "PhotosUI"
# "PrintCore" "QTKit" "QuickLook" "QuickLookUI" "ReplayKit" "SafariServices"
# "SceneKit" "ScriptingBridge" "SearchKit"
#  "Security" "SensorKit" "Simd" "Social" "SpriteKit" "StoreKit" "TVMLKit" "TVServices" "Twitter" 
declare -a apis=("UIKit" "UserNotifications" "VideoSubscriberAccount" "VideoToolbox" "Vision" "WKWebKit" "WatchConnectivity" "WatchKit" "WebKit" "XKit" "iAd" "iTunesLibrary")

set -e
set -u
set -o pipefail

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

for i in "${apis[@]}"
do
   echo "$i"
   # or do whatever with individual element of the array

   SDK_PATH=~/Programming/xamarin-macios/src/$i
   dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --detect-unresolvable
   dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --strip-attributes
   dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --strip-blocks
   dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --harvest-assembly=Xamarin.iOS.dll --add-default-introduced
done