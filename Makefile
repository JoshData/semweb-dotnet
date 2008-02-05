VERSION=1.03
    # don't forget to update src/AssemblyInfo.cs!!

########################

# Some checks to see if dependenies are available for
# optional assemblies.

npgsql_available := $(shell gacutil -l Npgsql | grep -c PublicKeyToken)
sqlite_available := $(shell gacutil -l Mono.Data.SqliteClient | grep -c PublicKeyToken)
mysql_available := $(shell gacutil -l MySql.Data | grep -c PublicKeyToken)

########################

# If PROFILE is empty, then our default target is to
# shell out a make for each profile.  Otherwise, we
# just build the profile we're given.  Since we can't
# build any of the .NET target files without a PROFILE,
# those targets are in the else condition.
ifeq "$(PROFILE)" ""

all:
	PROFILE=DOTNET1 make
	PROFILE=DOTNET2 make
#	PROFILE=SILVERLIGHT make
#	PROFILE=DOTNET3 make

# If we have a PROFILE specified.
else

ifeq "$(PROFILE)" "DOTNET1"
BIN=bin
MCS=mcs -d:DOTNET1
endif

ifeq "$(PROFILE)" "DOTNET2"
BIN=bin_generics
MCS=gmcs -d:DOTNET2
endif

ifeq "$(PROFILE)" "DOTNET3"
BIN=bin_linq
MCS=gmcs -d:DOTNET3 -langversion:linq
endif

ifeq "$(PROFILE)" "SILVERLIGHT"
BIN=bin_silverlight
MCS=smcs -d:SILVERLIGHT
endif

all: $(BIN)/SemWeb.dll $(BIN)/SemWeb.PostgreSQLStore.dll $(BIN)/SemWeb.MySQLStore.dll $(BIN)/SemWeb.SqliteStore.dll $(BIN)/SemWeb.Sparql.dll $(BIN)/rdfstorage.exe $(BIN)/rdfquery.exe $(BIN)/euler.exe

# Core Library
	
MAIN_SOURCES = \
	src/AssemblyInfo.cs \
	src/NamespaceManager.cs src/Util.cs src/UriMap.cs \
	src/Resource.cs src/Statement.cs \
	src/Interfaces.cs \
	src/Store.cs src/MemoryStore.cs src/SQLStore.cs \
	src/RdfReader.cs src/RdfXmlReader.cs src/N3Reader.cs \
	src/RdfWriter.cs src/RdfXmlWriter.cs src/N3Writer.cs \
	src/Query.cs src/GraphMatch.cs src/LiteralFilters.cs \
	src/Inference.cs src/RDFS.cs src/Euler.cs src/SpecialRelations.cs \
	src/Algos.cs src/SparqlClient.cs

$(BIN)/SemWeb.dll: $(MAIN_SOURCES) Makefile
	mkdir -p $(BIN)
	$(MCS) -debug $(MAIN_SOURCES) -out:$(BIN)/SemWeb.dll -t:library \
		-r:System.Data -r:System.Web

# Auxiliary Assemblies

$(BIN)/SemWeb.Sparql.dll: src/SparqlEngine.cs src/SparqlProtocol.cs
	$(MCS) -debug src/SparqlEngine.cs src/SparqlProtocol.cs -out:$(BIN)/SemWeb.Sparql.dll \
		-t:library -r:$(BIN)/SemWeb.dll -r:$(BIN)/sparql-core.dll -r:$(BIN)/IKVM.GNU.Classpath.dll \
		-r:System.Web

$(BIN)/SemWeb.PostgreSQLStore.dll: src/PostgreSQLStore.cs
ifneq "$(npgsql_available)" "0"
	$(MCS) -debug src/PostgreSQLStore.cs -out:$(BIN)/SemWeb.PostgreSQLStore.dll -t:library \
		-r:$(BIN)/SemWeb.dll -r:System.Data -r:Npgsql
else
	@echo "SKIPPING compilation of SemWeb.PosgreSQLStore.dll because Npgsql assembly seems to be not available in the GAC.";
endif

$(BIN)/SemWeb.SqliteStore.dll: src/SQLiteStore.cs
ifneq "$(sqlite_available)" "0"
	$(MCS) -debug src/SQLiteStore.cs -out:$(BIN)/SemWeb.SqliteStore.dll -t:library \
		-r:$(BIN)/SemWeb.dll -r:System.Data -r:Mono.Data.SqliteClient
else
	@echo "SKIPPING compilation of SemWeb.SqliteStore.dll because Mono.Data.SqliteClient assembly seems to be not available in the GAC.";
endif
	
$(BIN)/SemWeb.MySQLStore.dll: src/MySQLStore.cs
ifneq "$(PROFILE)" "DOTNET1" # the MySql.Data lib we are compiling against is 2.0.
ifneq "$(mysql_available)" "0"
	$(MCS) -debug src/MySQLStore.cs -out:$(BIN)/SemWeb.MySQLStore.dll -t:library\
		 -r:$(BIN)/SemWeb.dll -r:System.Data -r:MySql.Data -d:CONNECTOR -lib:lib
	#$(MCS) -debug src/MySQLStore.cs -out:$(BIN)/SemWeb.MySQLStore-ByteFX.dll -t:library\
	# -r:$(BIN)/SemWeb.dll -r:System.Data -r:ByteFX.Data -d:BYTEFX
else
	@echo "SKIPPING compilation of SemWeb.MySQLStore.dll because MySql.Data assembly seems to be not available in the GAC.";
endif
endif

# Utility programs

$(BIN)/rdfstorage.exe: tools/rdfstorage.cs
	$(MCS) -debug tools/rdfstorage.cs -out:$(BIN)/rdfstorage.exe -r:$(BIN)/SemWeb.dll -r:Mono.GetOptions
	
$(BIN)/rdfquery.exe: tools/rdfquery.cs
	$(MCS) -debug tools/rdfquery.cs -out:$(BIN)/rdfquery.exe -r:$(BIN)/SemWeb.dll -r:$(BIN)/SemWeb.Sparql.dll -r:Mono.GetOptions	

$(BIN)/euler.exe: tools/euler.cs
	$(MCS) -debug tools/euler.cs -out:$(BIN)/euler.exe -r:$(BIN)/SemWeb.dll -r:$(BIN)/SemWeb.Sparql.dll

endif
# that's the end of the test if we have a PROFILE given

# Generating documentation files

apidocxml: Makefile
	monodocer \
		-assembly:bin_generics/SemWeb.dll -assembly:bin_generics/SemWeb.Sparql.dll \
		-path:apidocxml --delete --pretty
	#mono /usr/lib/monodoc/monodocs2slashdoc.exe doc > SemWeb.docs.xml
	mkdir -p apidocs
	monodocs2html -source:apidocxml -dest:apidocs -template:docstemplate.xsl

# Generating the release package

package: all
	rm -rf package-workspace
	mkdir -p package-workspace/semweb-$(VERSION)
	cp -R bin bin_generics bin_silverlight src tools apidocs doc \
		ChangeLog Makefile README.txt semweb.mds semweb.sln \
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
	scp packages/semweb-$(VERSION).tgz packages/semweb.zip occams.info:www/code/semweb

clean:
	rm bin*/SemWeb.dll* bin*/SemWeb.Sparql.dll* \
	bin*/SemWeb.PostgreSQLStore.dll* bin*/SemWeb.SqliteStore.dll* bin*/SemWeb.MySQLStore.dll* \
	bin*/rdfstorage.exe* bin*/rdfquery.exe* bin*/euler.exe*
