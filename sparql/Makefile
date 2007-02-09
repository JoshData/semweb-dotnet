SPARQL=work-copy
SPARQL_RELEASE=upstream

JAVA_HOME=/usr/java/jdk1.5.0_04
IKVM=ikvm-0.28.0.0/bin/ikvmc.exe
ANT=~/dev/apache-ant-1.6.5/bin/ant

all: sparql-core.dll local-changes.diff

$(SPARQL)/lib/sparql-core.jar:
	cd $(SPARQL); $(ANT) jar;

sparql-core.dll: $(SPARQL)/lib/sparql-core.jar
	mono $(IKVM) -out:sparql-core.dll \
		$(SPARQL)/lib/sparql-core.jar $(SPARQL)/lib/openrdf-util.jar \
		$(SPARQL)/lib/openrdf-model.jar
	mono --aot -O=all sparql-core.dll
	cp sparql-core.dll* ../bin
	cp sparql-core.dll* ../bin_generics

linked-libs/sparql-core.dll: sparql-core.dll
	echo "<linker>" > sparql-linker.xml
	echo "<assembly fullname='sparql-core'>" >> sparql-linker.xml
	monop -r:sparql-core.dll|egrep "org|name"| sed -e "s/^/<type fullname='/" -e "s/$$/'\/>/" >> sparql-linker.xml
	echo "</assembly>" >> sparql-linker.xml
	echo "</linker>" >> sparql-linker.xml
	mono ~/dev/mono/cecil/linker/linker.exe -s true -out linked-libs -x sparql-linker.xml

local-changes.diff:
	diff -urN $(SPARQL_RELEASE) $(SPARQL) --exclude=.svn --exclude=.settings --exclude=META-INF > local-changes.diff || true
	
	#$(JAVA_HOME)/bin/javac Tester.java -classpath $(SPARQL)/lib/sparql-core.jar
