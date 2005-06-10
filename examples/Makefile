all: *.cs
	ln -sf ../bin/SemWeb.dll .;
	for sample in `ls *.cs`; do mcs -g $$sample -r:SemWeb.dll ; done
