VERSION=0.504

all: bin/SemWeb.dll bin/SemWeb.MySQLStore.dll bin/SemWeb.SqliteStore.dll bin/SemWeb.Sparql.dll bin/rdfstorage.exe bin/rdfquery.exe
	
# The core library

bin/SemWeb.dll: src/*.cs
	mcs -debug src/*.cs -out:bin/SemWeb.dll -t:library \
		-r:System.Data

bin/SemWeb.Sparql.dll: sparql/*.cs
	cp sparql/lib/* bin
	mcs -debug sparql/*.cs -out:bin/SemWeb.Sparql.dll \
		-t:library -r:bin/SemWeb.dll -r:bin/sparql-core.dll -r:bin/IKVM.GNU.Classpath.dll

# SQL store backends

bin/SemWeb.SqliteStore.dll: src.misc/SQLiteStore.cs bin/SemWeb.dll
	mcs -debug src.misc/SQLiteStore.cs -out:bin/SemWeb.SqliteStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:Mono.Data.SqliteClient
	
bin/SemWeb.MySQLStore.dll: src.misc/MySQLStore.cs bin/SemWeb.dll
	mcs -debug src.misc/MySQLStore.cs -out:bin/SemWeb.MySQLStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:ByteFX.Data

# Utility programs

bin/rdfstorage.exe: src.misc/rdfstorage.cs bin/SemWeb.dll
	mcs -debug src.misc/rdfstorage.cs -out:bin/rdfstorage.exe -r:bin/SemWeb.dll -r:Mono.GetOptions
	
bin/rdfquery.exe: src.misc/rdfquery.cs bin/SemWeb.dll
	mcs -debug src.misc/rdfquery.cs -out:bin/rdfquery.exe -r:bin/SemWeb.dll -r:bin/SemWeb.Sparql.dll -r:Mono.GetOptions	

# The following programs are experimental:

bin/rdfshmush.exe: src.misc/rdfshmush.cs bin/SemWeb.dll
	mcs -debug src.misc/rdfshmush.cs -out:bin/rdfshmush.exe -r bin/SemWeb.dll

bin/runtests.exe: src.misc/runtests.cs
	mcs -debug src.misc/runtests.cs -out:bin/runtests.exe -r bin/SemWeb.dll

bin/rdfxsltproc.exe: src.misc/rdfxsltproc.cs
	mcs -debug src.misc/rdfxsltproc.cs -out:bin/rdfxsltproc.exe -r bin/SemWeb.dll -r Mono.GetOptions
		
bin/rdfbind.exe: src.misc/rdfbind.cs bin/SemWeb.dll
	mcs -debug src.misc/rdfbind.cs -out:bin/rdfbind.exe -r:bin/SemWeb.dll -r:Mono.GetOptions

# Generating documentation files

apidocxml: Makefile
	mono /usr/lib/monodoc/monodocer.exe \
		-assembly:bin/SemWeb.dll -assembly:bin/SemWeb.Sparql.dll \
		-path:apidocxml #--delete
	#mono /usr/lib/monodoc/monodocs2slashdoc.exe doc > SemWeb.docs.xml
	mkdir -p apidocs
	mono /usr/lib/monodoc/monodocs2html.exe -source:apidocxml -dest:apidocs -template:docstemplate.xsl

# Generating the release package

semweb.zip: bin/SemWeb.dll Makefile doc apidocs
	rm -f semweb.zip
	zip -r semweb.zip \
	bin/* \
	src/*.cs src.misc/*.cs \
	examples/*.cs examples/*.rdf \
	apidocsxml/*.xml apidocsxml/*/*.xml \
	apidocs \
	doc \
	Makefile README README.xhtml ChangeLog

semweb-$(VERSION).tgz: semweb.zip
	rm -rf package-workspace
	mkdir -p package-workspace/semweb-$(VERSION)
	unzip semweb.zip -d package-workspace/semweb-$(VERSION)
	tar -czf semweb-$(VERSION).tgz -C package-workspace semweb-$(VERSION)
	rm -rf package-workspace
	
deploy: semweb.zip semweb-$(VERSION).tgz
	scp semweb.zip semweb-$(VERSION).tgz publius:www/code/semweb
