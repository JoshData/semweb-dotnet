all: bin/test.exe bin/rdfs2cs.exe bin/SemWeb.MySQLStore.dll bin/SemWeb.SqliteStore.dll bin/SemWeb.dll bin/rdfstorage.exe bin/rdfquery.exe
	
bin/test.exe: test.cs bin/SemWeb.dll bin/SemWeb.SqliteStore.dll
	mcs test.cs -out:bin/test.exe \
	-r:bin/SemWeb.dll -r:bin/SemWeb.SqliteStore.dll

bin/rdfs2cs.exe: src.misc/rdfscs.cs #bin/SemWeb.dll
	mcs src.misc/rdfscs.cs -out:bin/rdfs2cs.exe -r:bin/SemWeb.dll -r:Mono.GetOptions

bin/rdfstorage.exe: src.misc/rdfstorage.cs #bin/SemWeb.dll
	mcs src.misc/rdfstorage.cs -out:bin/rdfstorage.exe -r:bin/SemWeb.dll -r:Mono.GetOptions
	
bin/rdfquery.exe: src.misc/rdfquery.cs #bin/SemWeb.dll
	mcs src.misc/rdfquery.cs -out:bin/rdfquery.exe -r:bin/SemWeb.dll -r:Mono.GetOptions	

bin/SemWeb.SqliteStore.dll: src.misc/SQLiteStore.cs
	mcs src.misc/SQLiteStore.cs -out:bin/SemWeb.SqliteStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:Mono.Data.SqliteClient
	
bin/SemWeb.MySQLStore.dll: src.misc/MySQLStore.cs
	mcs src.misc/MySQLStore.cs -out:bin/SemWeb.MySQLStore.dll -t:library\
	-r:bin/SemWeb.dll -r:System.Data -r:ByteFX.Data

bin/SemWeb.dll: src/*.cs
	cp lib/* bin
	mcs -g src/*.cs -out:bin/SemWeb.dll -t:library \
		-r:bin/Drive.dll -r:System.Data

