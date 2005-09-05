using System;
using System.IO;
using System.Reflection;

using SemWeb;
using SemWeb.Stores;
using SemWeb.Query;

[assembly: AssemblyTitle("RDFQuery - Query RDF Data")]
[assembly: AssemblyCopyright("Copyright (c) 2005 Joshua Tauberer <tauberer@for.net>\nreleased under the GPL.")]
[assembly: AssemblyDescription("A tool for querying RDF data.")]

[assembly: Mono.UsageComplement("data1 data2 ... < query.rdf")]

public class RDFQuery {
	private class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("The {type} of the query: 'match' to match the N3 graph with the target data or 'sparql' to run a SPARQL SELECT query on the target data.")]
		public string type = "match";
		
		[Mono.GetOptions.Option("The {format} for variable binding output (currently only 'xml').")]
		public string format = "xml";
		
		[Mono.GetOptions.Option("Maximum number of results to report.")]
		public int limit = 0;
	}

	public static void Main(string[] args) {
		Opts opts = new Opts();
		opts.ProcessArgs(args);

		if (opts.RemainingArguments.Length == 0) {
			opts.DoHelp();
			return;
		}
		
		string baseuri = "query://query/#";

		QueryResultSink qs;
		if (opts.format == "simple")
			qs = new PrintQuerySink();
		else if (opts.format == "sql")
			qs = new SQLQuerySink(Console.Out, "rdf");
		else if (opts.format == "html")
			qs = new HTMLQuerySink(Console.Out);
		else if (opts.format == "xml") {
			qs = new SparqlXmlQuerySink(Console.Out);
		} else {
			Console.Error.WriteLine("Invalid output format.");
			return;
		}

		Query query;
		if (opts.type == "rsquary") {
			RdfReader queryparser = RdfReader.Create("n3", "-");
			queryparser.BaseUri = baseuri;
			query = new RSquary(queryparser);
		} else if (opts.type == "sparql") {
			query = new Sparql(Console.In);
		} else {
			throw new Exception("Invalid query format: " + opts.type);
		}

		KnowledgeModel model = new KnowledgeModel();
		foreach (string arg in opts.RemainingArguments) {
			StatementSource source = Store.CreateForInput(arg);
			Store storage;
			if (source is Store) storage = (Store)source;
			else storage = new MemoryStore(source);
			model.Add(storage);
		}
		
		if (opts.limit > 0)
			query.ReturnLimit = opts.limit;

		//Console.Error.WriteLine(query.GetExplanation());
		
		query.Run(model, qs);
		
		if (qs is IDisposable)
			((IDisposable)qs).Dispose();
	}
}

