Q=$(if $(V),,@)

.PHONY: run test

run::
	$(Q) dotnet run --nologo --project src/mellite.csproj

test::
	$(Q) dotnet test --nologo test/mellite.tests.csproj