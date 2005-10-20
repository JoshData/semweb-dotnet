all: *.cs
	ln -sf ../bin/SemWeb.dll .;
	for sample in `ls *.cs`; do mcs -debug $$sample -r:SemWeb.dll ; done
