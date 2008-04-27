run-tests.exe: run-tests.cs
	MONO_PATH=../../bin mcs run-tests.cs -r:SemWeb.dll -r:SemWeb.Sparql -debug

run:
	MONO_PATH=../../bin mono --debug run-tests.exe test-suite-archive/data-r2/*/manifest.ttl
