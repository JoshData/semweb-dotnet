VERSION=0.601

all: bin/SemWeb.dll bin/SemWeb.MySQLStore.dll bin/SemWeb.SqliteStore.dll bin/SemWeb.Sparql.dll bin/rdfstorage.exe bin/rdfquery.exe

# Core Library
	
MAIN_SOURCES = \
	src/AssemblyInfo.cs \
	src/NamespaceManager.cs src/Util.cs src/UriMap.cs \
	src/Resource.cs src/Statement.cs \
	src/Store.cs src/MemoryStore.cs src/SQLStore.cs \
	src/RdfParser.cs src/XmlParser.cs src/N3Parser.cs \
	src/RdfWriter.cs src/XmlWriter.cs src/N3Writer.cs \
	src/RSquary.cs src/RSquaryFilters.cs src/Query.cs \
	src/XPathSemWebNavigator.cs

bin/SemWeb.dll: $(MAIN_SOURCES) Makefile
	mcs -debug $(MAIN_SOURCES) -out:bin/SemWeb.dll -t:library \
		-r:System.Data

# Auxiliary Assemblies

bin/SemWeb.Sparql.dll: src/Sparql.cs
	mcs -debug src/Sparql.cs -out:bin/SemWeb.Sparql.dll \
		-t:library -r:bin/SemWeb.dll -r:bin/sparql-core.dll -r:bin/IKVM.GNU.Classpath.dll

bin/SemWeb.SqliteStore.dll: src/SQLiteStore.cs bin/SemWeb.dll
	mcs -debug src/SQLiteStore.cs -out:bin/SemWeb.SqliteStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:Mono.Data.SqliteClient
	
bin/SemWeb.MySQLStore.dll: src/MySQLStore.cs bin/SemWeb.dll
	mcs -debug src/MySQLStore.cs -out:bin/SemWeb.MySQLStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:ByteFX.Data

# Utility programs

bin/rdfstorage.exe: tools/rdfstorage.cs bin/SemWeb.dll
	mcs -debug tools/rdfstorage.cs -out:bin/rdfstorage.exe -r:bin/SemWeb.dll -r:Mono.GetOptions
	
bin/rdfquery.exe: tools/rdfquery.cs bin/SemWeb.dll
	mcs -debug tools/rdfquery.cs -out:bin/rdfquery.exe -r:bin/SemWeb.dll -r:bin/SemWeb.Sparql.dll -r:Mono.GetOptions	

# Generating documentation files

apidocxml: Makefile
	mono /usr/lib/monodoc/monodocer.exe \
		-assembly:bin/SemWeb.dll -assembly:bin/SemWeb.Sparql.dll \
		-path:apidocxml #--delete
	#mono /usr/lib/monodoc/monodocs2slashdoc.exe doc > SemWeb.docs.xml
	mkdir -p apidocs
	mono /usr/lib/monodoc/monodocs2html.exe -source:apidocxml -dest:apidocs -template:docstemplate.xsl

# Generating the release package

package: all
	rm -rf package-workspace
	mkdir -p package-workspace/semweb-$(VERSION)
	cp -R bin src tools apidocs \
		ChangeLog Makefile README.txt semweb.mds \
		package-workspace/semweb-$(VERSION)
	mkdir package-workspace/semweb-$(VERSION)/examples
	cp examples/*.cs examples/Makefile examples/README.txt \
		package-workspace/semweb-$(VERSION)/examples
	tar -czf packages/semweb-$(VERSION).tgz -C package-workspace \
		--exclude .svn \
		semweb-$(VERSION)
	rm -rf package-workspace
	
deploy: package
	scp packages/semweb-$(VERSION).tgz publius:www/code/semweb
