run-tests.exe: run-tests.cs
	mcs run-tests.cs -r:SemWeb.dll -r:SemWeb.Sparql -debug

run:
	mono --debug run-tests.exe test-suite-archive/data-r2/*/manifest.ttl
