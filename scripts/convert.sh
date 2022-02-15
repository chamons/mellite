#!/bin/bash

set -e
set -u
set -o pipefail

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
SDK_PATH=~/Programming/xamarin-macios/src/$1

echo "Checking for unresolvable first..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --detect-unresolvable
echo "Clear. Stripping Existing NET6 Attributes..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --strip-attributes
echo "Atttribute Strip Complete. Stripping unnecessary blocks..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --strip-blocks
echo "Completed - Please review and commit."
read _
echo "Running final conversion..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build $SDK_PATH --harvest-assembly=Xamarin.Mac.dll --add-default-introduced=$SCRIPT_DIR/../
echo "Completed - Please review and commit."
read _