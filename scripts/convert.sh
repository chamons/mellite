#!/bin/bash

set -e
set -u
set -o pipefail

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

echo "Checking for unresolvable first..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/$1/ --detect-unresolvable
echo "Clear. Stripping Existing NET6 Attributes..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/$1/ --strip-attributes
echo "Atttribute Strip Complete. Stripping unnecessary blocks..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/$1/ --strip-blocks
echo "Completed - Please review and commit."
read _
echo "Running final conversion..."
dotnet run --project $SCRIPT_DIR/../src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/$1/
echo "Completed - Please review and commit."
read _