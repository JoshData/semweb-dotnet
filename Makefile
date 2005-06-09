all: bin/SemWeb.dll bin/SemWeb.MySQLStore.dll bin/SemWeb.SqliteStore.dll bin/SemWeb.dll bin/rdfstorage.exe bin/rdfquery.exe bin/test.exe bin/rdfs2cs.exe
#bin/rdfshmush.exe bin/rdfs2cs.exe 
	
bin/test.exe: test.cs bin/SemWeb.dll bin/SemWeb.SqliteStore.dll
	mcs test.cs -out:bin/test.exe \
	-r:bin/SemWeb.dll -r:bin/SemWeb.SqliteStore.dll

bin/rdfs2cs.exe: src.misc/rdfscs.cs bin/SemWeb.dll
	mcs src.misc/rdfscs.cs -out:bin/rdfs2cs.exe -r:bin/SemWeb.dll -r:Mono.GetOptions

bin/rdfstorage.exe: src.misc/rdfstorage.cs bin/SemWeb.dll
	mcs src.misc/rdfstorage.cs -out:bin/rdfstorage.exe -r:bin/SemWeb.dll -r:Mono.GetOptions
	
bin/rdfquery.exe: src.misc/rdfquery.cs bin/SemWeb.dll
	mcs src.misc/rdfquery.cs -out:bin/rdfquery.exe -r:bin/SemWeb.dll -r:Mono.GetOptions	

bin/SemWeb.SqliteStore.dll: src.misc/SQLiteStore.cs bin/SemWeb.dll
	mcs src.misc/SQLiteStore.cs -out:bin/SemWeb.SqliteStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:Mono.Data.SqliteClient
	
bin/SemWeb.MySQLStore.dll: src.misc/MySQLStore.cs bin/SemWeb.dll
	mcs src.misc/MySQLStore.cs -out:bin/SemWeb.MySQLStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:ByteFX.Data

bin/SemWeb.dll: src/*.cs
	mcs -g src/*.cs -out:bin/SemWeb.dll -t:library \
		-r:System.Data

bin/rdfshmush.exe: src.misc/rdfshmush.cs bin/SemWeb.dll
	mcs src.misc/rdfshmush.cs -out:bin/rdfshmush.exe -r bin/SemWeb.dll
		
doc: Makefile
	mono /usr/lib/monodoc/monodocer.exe -assembly:bin/SemWeb.dll -path:doc #--delete
	#mono /usr/lib/monodoc/monodocs2slashdoc.exe doc > SemWeb.docs.xml
	mkdir -p doc-html
	mono /usr/lib/monodoc/monodocs2html.exe -source:doc -dest:doc-html -template:docstemplate.xsl

semweb.zip: bin/SemWeb.dll Makefile
	rm -f semweb.zip
	zip -r semweb.zip \
	bin/* \
	doc/*.xml doc/*/*.xml \
	src/*.cs src.misc/*.cs examples/*.cs \
	Makefile README README.xhtml ChangeLog
	
deploy: semweb.zip
	scp semweb.zip publius:www/code/semweb
