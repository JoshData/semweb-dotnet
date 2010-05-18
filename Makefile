VERSION=`grep AssemblyVersion src/AssemblyInfo_Shared.cs |sed "s/.assembly: AssemblyVersion..\(.*\).../\1/"`

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
	PROFILE=DOTNET2 make
	#PROFILE=SILVERLIGHT make

# If we have a PROFILE specified.
else

ifeq "$(PROFILE)" "DOTNET1"
BIN=bin_net11
MCS=mcs -d:DOTNET1
MCS_LIBS=-r:System.Data -r:System.Web
endif

ifeq "$(PROFILE)" "DOTNET2"
BIN=bin
MCS=gmcs -d:DOTNET2
MCS_LIBS=-r:System.Data -r:System.Web
endif

ifeq "$(PROFILE)" "SILVERLIGHT"
BIN=bin_silverlight
MCS=smcs -d:DOTNET2 -d:SILVERLIGHT
MCS_LIBS=
endif

ifneq "$(PROFILE)" "SILVERLIGHT"  # auxiliary assemblies aren't compiled in the Silverlight build
all: $(BIN)/SemWeb.dll $(BIN)/SemWeb.PostgreSQLStore.dll $(BIN)/SemWeb.MySQLStore.dll $(BIN)/SemWeb.SqliteStore.dll $(BIN)/SemWeb.SQLServerStore.dll $(BIN)/SemWeb.Sparql.dll $(BIN)/rdfstorage.exe $(BIN)/rdfquery.exe $(BIN)/euler.exe
else
all: $(BIN)/SemWeb.dll
endif

# Core Library
	
MAIN_SOURCES = \
	src/AssemblyInfo_Shared.cs \
	src/AssemblyInfo.cs \
	src/Constants.cs \
	src/NamespaceManager.cs src/Util.cs src/UriMap.cs \
	src/Resource.cs src/Statement.cs \
	src/Interfaces.cs \
	src/Store.cs src/MemoryStore.cs src/SQLStore.cs \
	src/RdfReader.cs src/RdfXmlReader.cs src/N3Reader.cs \
	src/RdfWriter.cs src/RdfXmlWriter.cs src/N3Writer.cs \
	src/Query.cs src/GraphMatch.cs src/LiteralFilters.cs \
	src/Inference.cs src/RDFS.cs src/Euler.cs src/SpecialRelations.cs \
	src/Algos.cs src/SparqlClient.cs \
	src/GraphVizWriter.cs \
	src/TurtleWriter.cs src/NTriplesWriter.cs

signing.key:
	sn -k signing.key

$(BIN)/SemWeb.dll: $(MAIN_SOURCES) Makefile signing.key
	mkdir -p $(BIN)
	$(MCS) -debug $(MAIN_SOURCES) $(MCS_LIBS) -out:$(BIN)/SemWeb.dll -t:library

# Auxiliary Assemblies

ifneq "$(PROFILE)" "SILVERLIGHT"  # auxiliary assemblies aren't compiled in the Silverlight build

$(BIN)/SemWeb.Sparql.dll: src/SparqlEngine.cs src/SparqlProtocol.cs src/AssemblyInfo_Shared.cs signing.key
	$(MCS) -debug src/SparqlEngine.cs src/SparqlProtocol.cs src/AssemblyInfo_Shared.cs -out:$(BIN)/SemWeb.Sparql.dll \
		-t:library -r:$(BIN)/SemWeb.dll -r:$(BIN)/sparql-core.dll -r:$(BIN)/IKVM.GNU.Classpath.dll \
		-r:System.Web

$(BIN)/SemWeb.PostgreSQLStore.dll: src/PostgreSQLStore.cs src/AssemblyInfo_Shared.cs signing.key
ifneq "$(npgsql_available)" "0"
	$(MCS) -debug src/PostgreSQLStore.cs src/AssemblyInfo_Shared.cs -out:$(BIN)/SemWeb.PostgreSQLStore.dll -t:library \
		-r:$(BIN)/SemWeb.dll -r:System.Data -r:Npgsql
else
	@echo "SKIPPING compilation of SemWeb.PosgreSQLStore.dll because Npgsql assembly seems to be not available in the GAC.";
endif

$(BIN)/SemWeb.SqliteStore.dll: src/SQLiteStore.cs src/AssemblyInfo_Shared.cs signing.key
ifneq "$(sqlite_available)" "0"
	$(MCS) -debug src/SQLiteStore.cs src/AssemblyInfo_Shared.cs -out:$(BIN)/SemWeb.SqliteStore.dll -t:library \
		-r:$(BIN)/SemWeb.dll -r:System.Data -r:Mono.Data.SqliteClient
else
	@echo "SKIPPING compilation of SemWeb.SqliteStore.dll because Mono.Data.SqliteClient assembly seems to be not available in the GAC.";
endif
	
$(BIN)/SemWeb.MySQLStore.dll: src/MySQLStore.cs src/AssemblyInfo_Shared.cs signing.key
ifneq "$(PROFILE)" "DOTNET1" # the MySql.Data lib we are compiling against is 2.0.
ifneq "$(mysql_available)" "0"
	$(MCS) -debug src/MySQLStore.cs src/AssemblyInfo_Shared.cs -out:$(BIN)/SemWeb.MySQLStore.dll -t:library\
		 -r:$(BIN)/SemWeb.dll -r:System.Data -r:MySql.Data -d:CONNECTOR -lib:lib
	#$(MCS) -debug src/MySQLStore.cs -out:$(BIN)/SemWeb.MySQLStore-ByteFX.dll -t:library\
	# -r:$(BIN)/SemWeb.dll -r:System.Data -r:ByteFX.Data -d:BYTEFX
else
	@echo "SKIPPING compilation of SemWeb.MySQLStore.dll because MySql.Data assembly seems to be not available in the GAC.";
endif
endif

$(BIN)/SemWeb.SQLServerStore.dll: src/SQLServerStore.cs src/AssemblyInfo_Shared.cs signing.key
	$(MCS) -debug src/SQLServerStore.cs src/AssemblyInfo_Shared.cs -out:$(BIN)/SemWeb.SQLServerStore.dll -t:library\
		 -r:$(BIN)/SemWeb.dll -r:System.Data
endif

# Utility programs

$(BIN)/rdfstorage.exe: tools/rdfstorage.cs src/AssemblyInfo_Shared.cs signing.key
	$(MCS) -debug tools/rdfstorage.cs -out:$(BIN)/rdfstorage.exe -r:$(BIN)/SemWeb.dll -r:bin/Mono.GetOptions.dll
	
$(BIN)/rdfquery.exe: tools/rdfquery.cs src/AssemblyInfo_Shared.cs signing.key
	$(MCS) -debug tools/rdfquery.cs -out:$(BIN)/rdfquery.exe -r:$(BIN)/SemWeb.dll -r:$(BIN)/SemWeb.Sparql.dll -r:bin/Mono.GetOptions.dll

$(BIN)/euler.exe: tools/euler.cs src/AssemblyInfo_Shared.cs signing.key
	$(MCS) -debug tools/euler.cs -out:$(BIN)/euler.exe -r:$(BIN)/SemWeb.dll -r:$(BIN)/SemWeb.Sparql.dll

endif
# that's the end of the test if we have a PROFILE given

# Generating documentation files

apidocxml: Makefile
	mdoc update -o apidocxml --delete --exceptions bin/SemWeb.dll bin/SemWeb.Sparql.dll
	#mono /usr/lib/monodoc/monodocs2slashdoc.exe doc > SemWeb.docs.xml
	mkdir -p apidocs
	mdoc export-html --out=apidocs --template=docstemplate.xsl apidocxml

# Generating the release package

package: all
	rm -rf package-workspace
	mkdir -p package-workspace/semweb-$(VERSION)
	cp -R bin_net11 bin src tools apidocs apidocxml doc \
		ChangeLog Makefile README.txt semweb.mds semweb.sln \
		package-workspace/semweb-$(VERSION)
	mkdir package-workspace/semweb-$(VERSION)/sparql
	cp -R \
		sparql/README.txt sparql/Makefile sparql/local-changes.diff \
		package-workspace/semweb-$(VERSION)/sparql
	mkdir package-workspace/semweb-$(VERSION)/examples
	cp examples/*.cs examples/Makefile examples/README.txt examples/getsomedata.sh \
		package-workspace/semweb-$(VERSION)/examples
	tar -czf packages/semweb-$(VERSION).tgz -C package-workspace \
		--exclude .svn \
		semweb-$(VERSION)
	rm -f packages/semweb.zip
	cd package-workspace/semweb-$(VERSION); zip -r -q ../../packages/semweb.zip * -x "*.svn*"
	rm -rf package-workspace
	
deploy: package
	scp packages/semweb-$(VERSION).tgz packages/semweb.zip occams.info:www/code/semweb

clean:
	rm bin*/SemWeb.dll* bin*/SemWeb.Sparql.dll* \
	bin*/SemWeb.PostgreSQLStore.dll* bin*/SemWeb.SqliteStore.dll* bin*/SemWeb.MySQLStore.dll* bin/SemWeb.SQLServerStore.dll* \
	bin*/rdfstorage.exe* bin*/rdfquery.exe* bin*/euler.exe*
