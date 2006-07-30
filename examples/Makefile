all: *.cs
	ln -sf ../bin/SemWeb.dll .;
	for sample in `ls *.cs`; do gmcs -debug $$sample -r:SemWeb.dll -r:SemWeb.Sparql.dll -lib:../bin ; done

runtests: all
	mono helloworld.exe > helloworld.output.rdf
