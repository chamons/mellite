SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/$1 --detect-unresolvable