SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/$1 --detect-unresolvable --verbose-conditional=$2 --harvest-assembly=/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/lib/mono/Xamarin.iOS/Xamarin.iOS.dll