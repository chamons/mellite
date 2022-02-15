Q=$(if $(V),,@)

.PHONY: run test build reset

build:
	$(Q) dotnet build --nologo

sample::
	$(Q) dotnet run --project src/mellite.csproj -- --ignore=build --strip-attributes sample/second.cs
	$(Q) dotnet run --project src/mellite.csproj -- --ignore=build --strip-blocks sample/second.cs
	$(Q) dotnet run --project src/mellite.csproj -- --ignore=build sample/second.cs

reset::
	$(Q) git checkout -- sample/

test::
	$(Q) dotnet test --nologo test/mellite.tests.csproj

CONVERT_CMD=dotnet run --project src/mellite.csproj -- --ignore=build --ignore-root ~/Programming/xamarin-macios/src/
convert::
	$(Q) $(CONVERT_CMD) --strip-verify
	$(Q) $(CONVERT_CMD) --strip-attributes
	$(Q) $(CONVERT_CMD) --strip-blocks
	$(Q) $(CONVERT_CMD) 

verify::
	$(Q) $(CONVERT_CMD) --strip-verify
