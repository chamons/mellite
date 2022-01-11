SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src --strip-verify --harvest-assembly=/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/lib/mono/Xamarin.iOS/Xamarin.iOS.dll