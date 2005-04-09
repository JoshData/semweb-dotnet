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
	
	private static SqliteStore GetSqliteStore(KnowledgeModel model) {
		return new SqliteStore("URI=file:SqliteTest.db", "rdf", model);
	}
	
	public static void Main2(string[] args) {
		KnowledgeModel model = new KnowledgeModel();
		Store storage = new MemoryStore(model); // new SqliteStore("URI=file:SqliteTest.db", "rdf", model); // new MemoryStore(model);
		model.Storage.Add(storage);
		//storage.Import(new N3Parser("data.n3"));
		storage.Import(new RdfXmlParser("../rdf/schemas/foaf.rdf"));
		//model.AddReasoning(new RDFSReasoning());

		RdfCompactWriter writer = new RdfCompactWriter(Console.Out);
		storage.Write(writer);
		return;
		
		MemoryStore queryfile = new MemoryStore(null);
		RdfParser qp = new RdfXmlParser("query.rdf");
		qp.BaseUri = "query://query/";
		queryfile.Import(qp);
		
		RSquary query = new RSquary(queryfile, "query://query/#query");
		
		for (int i = 0; i < 3; i++) {
			DateTime start = DateTime.Now;
			query.Query(model, new PrintQuerySink());
			Console.Error.WriteLine(DateTime.Now - start);
		}
	}
	
	public static void Main(string[] args) {
		KnowledgeModel model = new KnowledgeModel();

		MemoryStore storage = new MemoryStore(model);
		model.Storage.Add(storage);
		storage.Import(new RdfXmlParser(new StreamReader("../rdf/data/people.rdf")));
		
		XPathSemWebNavigator nav = new XPathSemWebNavigator(storage.GetResource("urn://govshare.info/data/us/congress/people/1995/akaka"), GetNSMgr());
		
		//System.Xml.XPath.XPathNodeIterator iter = nav.SelectChildren("type", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
		System.Xml.XPath.XPathNodeIterator iter = nav.Select("rdf:type");
		while (iter.MoveNext())
			Console.WriteLine(iter.Current);

	}	
}
