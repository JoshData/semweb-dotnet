using System;
using System.Collections;
using System.IO;

using SemWeb;
using SemWeb.IO;
using SemWeb.Stores;
using SemWeb.Query;

public class Test {
	private static NamespaceManager GetNSMgr() {
		NamespaceManager ns = new NamespaceManager();
		ns.AddNamespace("http://www.w3.org/1999/02/22-rdf-syntax-ns#", "rdf");
		ns.AddNamespace("http://www.w3.org/2000/01/rdf-schema#", "rdfs");
		ns.AddNamespace("http://purl.org/dc/elements/1.1/", "dc");
		ns.AddNamespace("http://xmlns.com/foaf/0.1/", "foaf");
		ns.AddNamespace("urn:govshare.info/rdf/politico/", "politico");
		return ns;		
	}
	
	private static SqliteStore GetSqliteStore() {
		return new SqliteStore("URI=file:SqliteTest.db", "rdf");
	}
	
	public static void Main(string[] args) {
		MemoryStore storage = new MemoryStore();
		storage.Import(new N3Parser(new StreamReader("people.n3")));
		

	}	
}
