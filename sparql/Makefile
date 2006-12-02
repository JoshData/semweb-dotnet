SPARQL=work-copy
SPARQL_RELEASE=upstream

JAVA_HOME=/usr/java/jdk1.5.0_04
IKVM=ikvm-0.28.0.0/bin/ikvmc.exe
ANT=~/dev/apache-ant-1.6.5/bin/ant

all:
	cd $(SPARQL); $(ANT) jar;
	mono $(IKVM) -out:sparql-core.dll \
		$(SPARQL)/lib/sparql-core.jar $(SPARQL)/lib/openrdf-util.jar \
		$(SPARQL)/lib/openrdf-model.jar
	mv sparql-core.dll ../bin
	mono --aot -O=all ../bin/sparql-core.dll
	diff -urN $(SPARQL_RELEASE) $(SPARQL) --exclude=.svn --exclude=.settings --exclude=META-INF > local-changes.diff || true
	
	#$(JAVA_HOME)/bin/javac Tester.java -classpath $(SPARQL)/lib/sparql-core.jar
