using System;
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
	
	private static SqliteStore GetSqliteStore(KnowledgeModel model) {
		return new SqliteStore("URI=file:SqliteTest.db", "rdf", model);
	}
	
	/*private static void Recurse(XPathSemWebNavigator nav, string indent) {
		nav = (XPathSemWebNavigator)nav.Clone();
		Console.Write(indent);
		Console.WriteLine(nav.NamespaceURI + nav.LocalName + " = " + nav.Value);
		if (!nav.MoveToFirst()) return;
		while (true) {
			Recurse(nav, indent + " ");
			if (!nav.MoveToNext()) break;
		}
	}*/
	
	public static void Main(string[] args) {
		KnowledgeModel model = new KnowledgeModel();
		Store storage = new MemoryStore(model); // new SqliteStore("URI=file:SqliteTest.db", "rdf", model); // new MemoryStore(model);
		model.Storage.Add(storage);
		storage.Import(new N3Parser("data.n3"));
		storage.Import(new RdfXmlParser("../rdf/schemas/foaf.rdf"));
		//model.AddReasoning(new RDFSReasoning());
		
		MemoryStore queryfile = new MemoryStore(null);
		RdfParser qp = new RdfXmlParser("query.rdf");
		qp.BaseUri = "query://query/";
		queryfile.Import(qp);
		
		//XPathSemWebNavigator nav = new XPathSemWebNavigator(queryfile.GetResource("query://query/#query"), queryfile, null);
		//Recurse(nav, "");

		RSquary query = new RSquary(queryfile, "query://query/#query");
		
		for (int i = 0; i < 3; i++) {
			DateTime start = DateTime.Now;
			query.Query(model, new PrintQuerySink());
			Console.Error.WriteLine(DateTime.Now - start);
		}
	}
	
	public static void Main2(string[] args) {
		KnowledgeModel model = new KnowledgeModel();

		MemoryStore storage = new MemoryStore(model);
		model.Storage.Add(storage);
		storage.Import(new N3Parser(new StreamReader("../rdf/people.tri")));
		
		RdfXmlWriter w = new RdfXmlWriter(Console.Out);
		w.Namespaces.AddNamespace("http://www.w3.org/2000/01/rdf-schema#", "rdfs");
		w.Namespaces.AddNamespace("http://www.agfa.com/w3c/euler/graph.axiom#", "graph");
		w.Namespaces.AddNamespace("http://xmlns.com/foaf/0.1/", "foaf");
		w.Namespaces.AddNamespace("urn:govshare.info/rdf/politico/", "pol");
		storage.Write(w);
		w.Close();
	}	
}
