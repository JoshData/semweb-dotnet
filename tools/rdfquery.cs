using System;
using System.IO;
using System.Reflection;
using System.Text;

using SemWeb;
using SemWeb.Stores;
using SemWeb.Query;

[assembly: AssemblyTitle("RDFQuery - Query RDF Data")]
[assembly: AssemblyCopyright("Copyright (c) 2006 Joshua Tauberer <http://razor.occams.info>\nreleased under the GPL.")]
[assembly: AssemblyDescription("A tool for querying RDF data.")]

[assembly: Mono.UsageComplement("n3:datafile.n3 < query.rdf")]

public class RDFQuery {
	private class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("The type of the query: '{rsquary}' to match the N3 graph with the target data or 'sparql' to run a SPARQL SELECT query on the target data.")]
		public string type = "rsquary";
		
		[Mono.GetOptions.Option("The format for variable binding output: {xml}, csv, html")]
		public string format = "xml";
		
		[Mono.GetOptions.Option("Maximum number of results to report.")]
		public int limit = 0;
	}

	public static void Main(string[] args) {
		System.Net.ServicePointManager.Expect100Continue = false;
	            
		Opts opts = new Opts();
		opts.ProcessArgs(args);

		if (opts.RemainingArguments.Length != 1) {
			opts.DoHelp();
			return;
		}
		
		string baseuri = "query://query/#";

		QueryResultSink qs;
		if (opts.format == "simple")
			qs = new PrintQuerySink();
		else if (opts.format == "html")
			qs = new HTMLQuerySink(Console.Out);
		else if (opts.format == "xml")
			qs = new SparqlXmlQuerySink(Console.Out);
		else if (opts.format == "lubm")
			qs = new LUBMReferenceAnswerOutputQuerySink();
		else if (opts.format == "csv")
			qs = new CSVQuerySink();
		else {
			Console.Error.WriteLine("Invalid output format.");
			return;
		}

		Query query;
		
		MemoryStore queryModel = null;
		#if !DOTNET2
		System.Collections.ICollection queryModelVars = null;
		#else
		System.Collections.Generic.ICollection<Variable> queryModelVars = null;
		#endif
		
		Store model = Store.Create(opts.RemainingArguments[0]);
		
		if (opts.type == "rsquary") {
			RdfReader queryparser = RdfReader.Create("n3", "-");
			queryparser.BaseUri = baseuri;
			queryModel = new MemoryStore(queryparser);
			queryModelVars = queryparser.Variables;
			query = new GraphMatch(queryModel);
		} else if (opts.type == "sparql" && model.DataSources.Count == 1 && model.DataSources[0] is SemWeb.Remote.SparqlSource) {
			string querystring = Console.In.ReadToEnd();
			((SemWeb.Remote.SparqlSource)model.DataSources[0]).RunSparqlQuery(querystring, Console.Out);
			return;
		} else if (opts.type == "sparql") {
			string querystring = Console.In.ReadToEnd();
			query = new SparqlEngine(querystring);
		} else {
			throw new Exception("Invalid query format: " + opts.type);
		}

		if (opts.limit > 0)
			query.ReturnLimit = opts.limit;

		//Console.Error.WriteLine(query.GetExplanation());
		
		if (query is SparqlEngine && ((SparqlEngine)query).Type != SparqlEngine.QueryType.Select) {
			SparqlEngine sparql = (SparqlEngine)query;
			sparql.Run(model, Console.Out);
		} else if (model is QueryableSource && queryModel != null) {
			SemWeb.Query.QueryOptions qopts = new SemWeb.Query.QueryOptions();
			qopts.DistinguishedVariables = queryModelVars;

			// Replace bnodes in the query with Variables
			int bnodectr = 0;
			foreach (Entity e in queryModel.GetEntities()) {
				if (e is BNode && !(e is Variable)) {
					BNode b = (BNode)e;
					queryModel.Replace(e, new Variable(b.LocalName != null ? b.LocalName : "bnodevar" + (++bnodectr)));
				}
			}
			
			model.Query(queryModel.ToArray(), qopts, qs);
		} else {
			query.Run(model, qs);
		}
		
		if (qs is IDisposable)
			((IDisposable)qs).Dispose();
	}
}


public class PrintQuerySink : QueryResultSink {
	public override bool Add(VariableBindings result) {
		foreach (Variable var in result.Variables)
			if (var.LocalName != null && result[var] != null)
				Console.WriteLine(var.LocalName + " ==> " + result[var].ToString());
		Console.WriteLine();
		return true;
	}
}

public class CSVQuerySink : QueryResultSink {
	public override void Init(Variable[] variables) {
		bool first = true;
		foreach (Variable var in variables) {
			if (var.LocalName == null) continue;
			if (!first) Console.Write(","); first = false;
			Console.Write(var.LocalName);
		}
		Console.WriteLine("");
	}
	public override bool Add(VariableBindings result) {
		bool first = true;
		foreach (Variable var in result.Variables) {
			if (var.LocalName == null) continue;
			if (!first) Console.Write(","); first = false;
			if (result[var] == null) continue;

			string t = result[var].ToString();
			if (result[var] is Literal) t = ((Literal)result[var]).Value;
			Console.Write(t);
		}
		Console.WriteLine();
		return true;
	}
}

public class HTMLQuerySink : QueryResultSink {
	TextWriter output;
	
	public HTMLQuerySink(TextWriter output) { this.output = output; }

	public override void Init(Variable[] variables) {
		output.WriteLine("<table>");
		output.WriteLine("<tr>");
		foreach (Variable var in variables) {
			if (var.LocalName == null) continue;
			output.WriteLine("<th>" + var.LocalName + "</th>");
		}
		output.WriteLine("</tr>");
	}
	
	public override void Finished() {
		output.WriteLine("</table>");
	}
	
	public override bool Add(VariableBindings result) {
		output.WriteLine("<tr>");
		foreach (Variable var in result.Variables) {
			if (var.LocalName == null) continue;
			Resource varTarget = result[var];
			string t = varTarget.ToString();
			if (varTarget is Literal) t = ((Literal)varTarget).Value;
			output.WriteLine("<td>" + t + "</td>");
		}
		output.WriteLine("</tr>");			
		return true;
	}
}

internal class LUBMReferenceAnswerOutputQuerySink : QueryResultSink {
	int[] varorder;

	public override void Init(Variable[] variables) {
		varorder = new int[variables.Length];
		string[] varnames = new string[variables.Length];
		
		for (int i = 0; i < variables.Length; i++) {
			varorder[i] = i;
			varnames[i] = variables[i].LocalName.ToUpper();
		}
		
		Array.Sort(varnames, varorder);
		
		for (int i = 0; i < varnames.Length; i++)
			Console.Write(varnames[i] + " ");
		Console.WriteLine();
	}
	public override bool Add(VariableBindings result) {
		foreach (int idx in varorder) {
			if (result.Variables[idx].LocalName != null && result.Values[idx] != null) {
				if (result.Values[idx].Uri != null)
					Console.Write(result.Values[idx].Uri + " ");
				else if (result.Values[idx] is Literal)
					Console.Write(((Literal)result.Values[idx]).Value + " ");
				else if (result.Values[idx] is BNode)
					Console.Write("(bnode) ");
				else
					Console.Write(result.Values[idx] + " ");
			}
		}
		Console.WriteLine();
		return true;
	}
}
