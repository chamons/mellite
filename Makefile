Q=$(if $(V),,@)

.PHONY: run test build reset

build:
	$(Q) dotnet build --nologo

sample:: reset
	$(Q) dotnet run --project src/mellite.csproj -- --ignore=build sample/

reset::
	$(Q) git checkout -- sample/

test::
	$(Q) dotnet test --nologo test/mellite.tests.csproj

convert::
	$(Q) dotnet run --project src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/ --strip-attributes
	$(Q) dotnet run --project src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/ --strip-blocks
	$(Q) dotnet run --project src/mellite.csproj -- --ignore=build ~/Programming/xamarin-macios/src/
