using System;
using System.IO;
using System.Reflection;

using SemWeb;
using SemWeb.Query;

[assembly: AssemblyTitle("RDFQuery - Query RDF Data")]
[assembly: AssemblyCopyright("Copyright (c) 2005 Joshua Tauberer <tauberer@for.net>\nreleased under the GPL.")]
[assembly: AssemblyDescription("A tool for querying RDF data.")]

[assembly: Mono.UsageComplement("data1 data2 ... < query.rdf")]

public class RDFQuery {
	private class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("The {format} for query input: xml or n3.")]
		public string @in = "xml";
		
		[Mono.GetOptions.Option("The {format} for variable binding output: simple, sql, or html")]
		public string format = "simple";
		
		[Mono.GetOptions.Option("Use RDFS reasoning.")]
		public bool rdfs = false;

		[Mono.GetOptions.Option("Use OWL reasoning.")]
		public bool owl = false;

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

		RdfParser queryparser = RdfParser.Create(opts.@in, "-");
		queryparser.BaseUri = "query://query/#";
		
		QueryResultSink qs;
		if (opts.format == "simple")
			qs = new PrintQuerySink();
		else if (opts.format == "sql")
			qs = new SQLQuerySink(Console.Out, "rdf");
		else if (opts.format == "html")
			qs = new HTMLQuerySink(Console.Out);
		else if (opts.format == "xml")
			qs = new SparqlXmlQuerySink(new System.Xml.XmlTextWriter(Console.Out), queryparser.BaseUri);
		else {
			Console.Error.WriteLine("Invalid output format.");
			return;
		}

		KnowledgeModel querymodel = new KnowledgeModel(queryparser);
		
		RSquary query = new RSquary(querymodel, "query://query/#query");
		
		// Make sure the ?abc variables in N3 are considered variables.
		foreach (Entity var in queryparser.Variables)
			query.Select(var);
		
		KnowledgeModel model = new KnowledgeModel();
		foreach (string arg in opts.RemainingArguments) {
			Store storage = Store.CreateForInput(arg);
			model.Add(storage);
		}
		
		if (opts.rdfs) model.AddReasoning(new SemWeb.Reasoning.RDFSReasoning());
		if (opts.owl) model.AddReasoning(new SemWeb.Reasoning.OWLReasoning());
		
		if (opts.limit > 0)
			query.ReturnLimit = opts.limit;
		
		query.Query(model, qs);
		
		if (qs is IDisposable)
			((IDisposable)qs).Dispose();
	}
}

