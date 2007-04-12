using System;

using SemWeb;
using SemWeb.Remote;
using SemWeb.Query;

public class Sparql1 {

	public static void Main() {
		string endpoint = "http://my.opera.com/community/sparql/sparql";
		
		string ex1 = "PREFIX foaf: <http://xmlns.com/foaf/0.1/>\n"
					+ "SELECT ?user ?blog ?user2 WHERE \n"
					+ "{ ?user foaf:nick \"chaals\" .\n"
					+ "  ?user foaf:weblog ?blog . \n"
					+ "  ?user foaf:knows ?user2 . } LIMIT 10 \n";

		string ex2 = "PREFIX foaf: <http://xmlns.com/foaf/0.1/>\n"
					+ "ASK WHERE \n"
					+ "{ ?user foaf:nick \"chaals\" .\n"
					+ "  ?user foaf:weblog ?blog . \n"
					+ "  ?user foaf:knows ?user2 . } \n";

		string ex3 = "PREFIX foaf: <http://xmlns.com/foaf/0.1/>\n"
					+ "CONSTRUCT { ?user foaf:knows ?user2 } WHERE \n"
					+ "{ ?user foaf:nick \"chaals\" .\n"
					+ "  ?user foaf:knows ?user2 . } LIMIT 10 \n";

					
		SparqlHttpSource source = new SparqlHttpSource(endpoint);
		
		Console.WriteLine("RunSparqlQuery(ex1, Console.Out):");
		source.RunSparqlQuery(ex1, Console.Out);
		Console.WriteLine();
		
		Console.WriteLine("RunSparqlQuery(ex1, SparqlXmlQuerySink):");
		source.RunSparqlQuery(ex1, new SparqlXmlQuerySink(Console.Out));
		Console.WriteLine();
		Console.WriteLine();

		Console.WriteLine("RunSparqlQuery(ex2, bool):");
		bool result;
		source.RunSparqlQuery(ex2, out result);
		Console.WriteLine(result);
		Console.WriteLine();

		Console.WriteLine("RunSparqlQuery(ex3, N3Writer):");
		using (N3Writer writer = new N3Writer(Console.Out))
			source.RunSparqlQuery(ex3, writer);
		Console.WriteLine();
		
		Console.WriteLine("Select(subject,__,__)");
		using (N3Writer writer = new N3Writer(Console.Out))
			source.Select(new Statement("http://my.opera.com/danbri/xml/foaf#me", null, null), writer);
		Console.WriteLine();
		
		Console.WriteLine("Query(...) A");
		Variable a = new Variable("a");
		QueryOptions qo = new QueryOptions();
		qo.Limit = 10;
		source.Query(new Statement[] {
			new Statement(a, "http://xmlns.com/foaf/0.1/nick", (Literal)"chaals"),
			new Statement(a, new Variable("b"), new Variable("c")),
			}, qo, new SparqlXmlQuerySink(Console.Out));
		Console.WriteLine();
		Console.WriteLine();

		Console.WriteLine("Query(...) B");
		QueryResultBuffer qb = new QueryResultBuffer();
		source.Query(new Statement[] {
			new Statement(a, "http://xmlns.com/foaf/0.1/nick", (Literal)"chaals"),
			new Statement(a, new Variable("b"), new Variable("c")),
			}, qo, qb);
		foreach (VariableBindings b in qb) {
			Console.WriteLine("a => " + b["a"]);
			Console.WriteLine("b => " + b["b"]);
			Console.WriteLine();
		}
		Console.WriteLine();
	}

}
