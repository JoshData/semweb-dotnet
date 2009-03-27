all: *.cs
	ln -sf ../bin/SemWeb.dll .;
	ln -sf ../bin/SemWeb.Sparql.dll .;
	ln -sf ../bin/sparql-core.dll .;
	ln -sf ../bin/IVKM.GNU.Classpath.dll .;
	for sample in `ls *.cs`; do gmcs -debug $$sample -r:SemWeb.dll -r:SemWeb.Sparql.dll ; done

runtests: all
	mono helloworld.exe > helloworld.output.rdf

clean:
	rm *.exe*
