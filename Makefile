Q=$(if $(V),,@)

.PHONY: run test

run::
	$(Q) dotnet run --project src/mellite.csproj -- sample/

test::
	$(Q) dotnet test --nologo test/mellite.tests.csproj