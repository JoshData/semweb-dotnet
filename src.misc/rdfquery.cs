using System;
using System.IO;
using System.Reflection;

using SemWeb;
using SemWeb.Query;

public class RDFQuery {
	public static void Main(string[] args) {
		if (args.Length == 0) {
			Console.Error.WriteLine("Specify sources as command-line arguments, and send the query on standard input.");
			return;
		}
		
		KnowledgeModel model = new KnowledgeModel();
		foreach (string arg in args) {
			Store storage = Store.CreateForInput(arg, model);
			model.Add(storage);
		}
		
		RdfParser queryparser = new RdfXmlParser(Console.In);
		queryparser.BaseUri = "query://query/";
		KnowledgeModel querymodel = new KnowledgeModel(queryparser);
		
		RSquary query = new RSquary(querymodel, "query://query/#query");
		query.Query(model, new PrintQuerySink());
	}
}

