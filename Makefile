VERSION=0.82
    # don't forget to update src/AssemblyInfo.cs!!

########################

# Some checks to see if dependenies are available for
# optional assemblies.

npgsql_available := $(shell gacutil -l Npgsql | grep -c PublicKeyToken)
sqlite_available := $(shell gacutil -l Mono.Data.SqliteClient | grep -c PublicKeyToken)
mysql_available := $(shell gacutil -l MySql.Data | grep -c PublicKeyToken)

########################

all: bin/SemWeb.dll bin/SemWeb.PostgreSQLStore.dll bin/SemWeb.MySQLStore.dll bin/SemWeb.SqliteStore.dll bin/SemWeb.Sparql.dll bin/rdfstorage.exe bin/rdfquery.exe bin/euler.exe

# Core Library
	
MAIN_SOURCES = \
	src/AssemblyInfo.cs \
	src/NamespaceManager.cs src/Util.cs src/UriMap.cs \
	src/Resource.cs src/Statement.cs \
	src/Interfaces.cs \
	src/Store.cs src/MemoryStore.cs src/SQLStore.cs \
	src/RdfReader.cs src/RdfXmlReader.cs src/N3Reader.cs \
	src/RdfWriter.cs src/RdfXmlWriter.cs src/N3Writer.cs \
	src/Query.cs src/GraphMatch.cs src/LiteralFilters.cs src/RSquary.cs \
	src/Inference.cs src/RDFS.cs src/Euler.cs src/SpecialRelations.cs \
	src/Algos.cs src/Remote.cs

bin/SemWeb.dll: $(MAIN_SOURCES) Makefile
	mcs -debug $(MAIN_SOURCES) -out:bin/SemWeb.dll -t:library \
		-r:System.Data -r:System.Web

# Auxiliary Assemblies

bin/SemWeb.Sparql.dll: src/Sparql.cs src/SparqlProtocol.cs
	mcs -debug src/Sparql.cs src/SparqlProtocol.cs -out:bin/SemWeb.Sparql.dll \
		-t:library -r:bin/SemWeb.dll -r:bin/sparql-core.dll -r:bin/IKVM.GNU.Classpath.dll \
		-r:System.Web

bin/SemWeb.PostgreSQLStore.dll: src/PostgreSQLStore.cs bin/SemWeb.dll
ifneq "$(npgsql_available)" "0"
	mcs -debug src/PostgreSQLStore.cs -out:bin/SemWeb.PostgreSQLStore.dll -t:library \
		-r:bin/SemWeb.dll -r:System.Data -r:Npgsql
else
	@echo "SKIPPING compilation of SemWeb.PosgreSQLStore.dll because Npgsql assembly seems to be not available in the GAC.";
endif

bin/SemWeb.SqliteStore.dll: src/SQLiteStore.cs bin/SemWeb.dll
ifneq "$(sqlite_available)" "0"
	mcs -debug src/SQLiteStore.cs -out:bin/SemWeb.SqliteStore.dll -t:library \
		-r:bin/SemWeb.dll -r:System.Data -r:Mono.Data.SqliteClient
else
	@echo "SKIPPING compilation of SemWeb.SqliteStore.dll because Mono.Data.SqliteClient assembly seems to be not available in the GAC.";
endif
	
bin/SemWeb.MySQLStore.dll: src/MySQLStore.cs bin/SemWeb.dll
ifneq "$(mysql_available)" "0"
	mcs -debug src/MySQLStore.cs -out:bin/SemWeb.MySQLStore.dll -t:library\
		 -r:bin/SemWeb.dll -r:System.Data -r:MySql.Data -d:CONNECTOR -lib:lib
	#mcs -debug src/MySQLStore.cs -out:bin/SemWeb.MySQLStore-ByteFX.dll -t:library\
	# -r:bin/SemWeb.dll -r:System.Data -r:ByteFX.Data -d:BYTEFX
else
	@echo "SKIPPING compilation of SemWeb.MySQLStore.dll because MySql.Data assembly seems to be not available in the GAC.";
endif

# Utility programs

bin/rdfstorage.exe: tools/rdfstorage.cs bin/SemWeb.dll
	mcs -debug tools/rdfstorage.cs -out:bin/rdfstorage.exe -r:bin/SemWeb.dll -r:Mono.GetOptions
	
bin/rdfquery.exe: tools/rdfquery.cs bin/SemWeb.dll
	mcs -debug tools/rdfquery.cs -out:bin/rdfquery.exe -r:bin/SemWeb.dll -r:bin/SemWeb.Sparql.dll -r:Mono.GetOptions	

bin/euler.exe: tools/euler.cs bin/SemWeb.dll
	mcs -debug tools/euler.cs -out:bin/euler.exe -r:bin/SemWeb.dll

# Generating documentation files

apidocxml: Makefile
	monodocer \
		-assembly:bin/SemWeb.dll -assembly:bin/SemWeb.Sparql.dll \
		-path:apidocxml --delete --pretty
	#mono /usr/lib/monodoc/monodocs2slashdoc.exe doc > SemWeb.docs.xml
	mkdir -p apidocs
	monodocs2html -source:apidocxml -dest:apidocs -template:docstemplate.xsl

# Generating the release package

package: all
	rm -rf package-workspace
	mkdir -p package-workspace/semweb-$(VERSION)
	cp -R bin src tools apidocs doc \
		ChangeLog Makefile README.txt semweb.mds \
		package-workspace/semweb-$(VERSION)
	mkdir package-workspace/semweb-$(VERSION)/examples
	cp examples/*.cs examples/Makefile examples/README.txt examples/getsomedata.sh \
		package-workspace/semweb-$(VERSION)/examples
	tar -czf packages/semweb-$(VERSION).tgz -C package-workspace \
		--exclude .svn \
		semweb-$(VERSION)
	rm -f packages/semweb.zip
	cd package-workspace/semweb-$(VERSION); cp -R ../../apidocxml monodoc; zip -r -q ../../packages/semweb.zip * -x "*.svn*"
	rm -rf package-workspace
	
deploy: package
	scp packages/semweb-$(VERSION).tgz packages/semweb.zip publius:www/code/semweb
