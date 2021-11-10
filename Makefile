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