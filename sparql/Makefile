SPARQL=work-copy
SPARQL_RELEASE=upstream

JAVA_HOME=/usr/lib/jvm/java-6-sun-1.6.0.06
IKVM_BIN=ikvm-0.34.0.2/bin
IKVM=$(IKVM_BIN)/ikvmc.exe
ANT=JAVA_HOME=$(JAVA_HOME) /usr/bin/ant

all: get-sources build-sparql

get-sources:
	if [ \! -e upstream ]; then svn co https://sparql.svn.sourceforge.net/svnroot/sparql/engine/trunk upstream; fi
	if [ \! -e work-copy ]; then svn export upstream/ work-copy; patch -p0 < local-changes.diff; fi
	mkdir -p ../bin ../bin_generics

build-sparql:
	diff -urN $(SPARQL_RELEASE) $(SPARQL) --exclude=.svn --exclude=.settings --exclude=META-INF --exclude=*.jj --exclude=*.rej | grep -v "Binary files "  > local-changes.diff || true
	sed -i "s/ upstream\// work-copy\//" local-changes.diff
	cd $(SPARQL); $(ANT) grammar jar;
	mono $(IKVM) -out:sparql-core.dll -nostacktraceinfo \
		-keyfile:../signing.key -version:`grep AssemblyVersion ../src/AssemblyInfo_Shared.cs |sed "s/.assembly: AssemblyVersion..\(.*\).../\1/"` \
		$(SPARQL)/dist/sparql-core.jar 
	MONO_PATH=.:$(IKVM_BIN) mono --aot -O=all sparql-core.dll
	cp sparql-core.dll* ../bin

linked-libs/sparql-core.dll: sparql-core.dll
	echo "<linker>" > sparql-linker.xml
	echo "<assembly fullname='sparql-core'>" >> sparql-linker.xml
	monop -r:sparql-core.dll|egrep "org|name"| sed -e "s/^/<type fullname='/" -e "s/$$/'\/>/" >> sparql-linker.xml
	echo "</assembly>" >> sparql-linker.xml
	echo "</linker>" >> sparql-linker.xml
	mono ~/dev/mono/cecil/linker/linker.exe -s true -out linked-libs -x sparql-linker.xml

	
	#$(JAVA_HOME)/bin/javac Tester.java -classpath $(SPARQL)/lib/sparql-core.jar
