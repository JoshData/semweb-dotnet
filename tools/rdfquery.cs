using System;
using System.IO;
using System.Reflection;
using System.Text;

using SemWeb;
using SemWeb.Stores;
using SemWeb.Query;

[assembly: AssemblyTitle("RDFQuery - Query RDF Data")]
[assembly: AssemblyCopyright("Copyright (c) 2005 Joshua Tauberer <tauberer@for.net>\nreleased under the GPL.")]
[assembly: AssemblyDescription("A tool for querying RDF data.")]

[assembly: Mono.UsageComplement("data1 data2 ... < query.rdf")]

public class RDFQuery {
	private class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("The type of the query: '{rsquary}' to match the N3 graph with the target data or 'sparql' to run a SPARQL SELECT query on the target data.")]
		public string type = "rsquary";
		
		[Mono.GetOptions.Option("The format for variable binding output (currently only '{xml}').")]
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
			query = new GraphMatch(queryparser);
		} else if (opts.type == "sparql") {
			query = new Sparql(Console.In);
		} else {
			throw new Exception("Invalid query format: " + opts.type);
		}

		MultiStore model = new MultiStore();
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
		
		if (query is Sparql && ((Sparql)query).Type != Sparql.QueryType.Select) {
			Sparql sparql = (Sparql)query;
			sparql.Execute(model, Console.Out);
		} else {
			query.Run(model, qs);
		}
		
		if (qs is IDisposable)
			((IDisposable)qs).Dispose();
	}
}


public class PrintQuerySink : QueryResultSink {
	public override bool Add(VariableBinding[] result) {
		foreach (VariableBinding var in result)
			if (var.Name != null && var.Target != null)
				Console.WriteLine(var.Name + " ==> " + var.Target.ToString());
		Console.WriteLine();
		return true;
	}
}

public class HTMLQuerySink : QueryResultSink {
	TextWriter output;
	
	public HTMLQuerySink(TextWriter output) { this.output = output; }

	public override void Init(VariableBinding[] variables) {
		output.WriteLine("<tr>");
		foreach (VariableBinding var in variables) {
			if (var.Name == null) continue;
			output.WriteLine("<th>" + var.Name + "</th>");
		}
		output.WriteLine("</tr>");
	}
	
	public override void Finished() { }
	
	public override bool Add(VariableBinding[] result) {
		output.WriteLine("<tr>");
		foreach (VariableBinding var in result) {
			if (var.Name == null) continue;
			string t = var.Target.ToString();
			if (var.Target is Literal) t = ((Literal)var.Target).Value;
			output.WriteLine("<td>" + t + "</td>");
		}
		output.WriteLine("</tr>");			
		return true;
	}
}

public class SQLQuerySink : QueryResultSink {
	TextWriter output;
	string table;
	
	public SQLQuerySink(TextWriter output, string table) { this.output = output; this.table = table; }
	
	public override void Finished() { }

	private string GetFieldType(string datatype) {
		switch (datatype) {
			case "http://www.w3.org/2001/XMLSchema#string":
			case "http://www.w3.org/2001/XMLSchema#normalizedString":
				return "TEXT";

			case "http://www.w3.org/2001/XMLSchema#float":
				return "FLOAT";
			
			case "http://www.w3.org/2001/XMLSchema#double":
				return "DOUBLE PRECISION";
			
			case "http://www.w3.org/2001/XMLSchema#decimal":
				return "DECIMAL";
			
			case "http://www.w3.org/2001/XMLSchema#integer":
			case "http://www.w3.org/2001/XMLSchema#nonPositiveInteger":
			case "http://www.w3.org/2001/XMLSchema#negativeInteger":
			case "http://www.w3.org/2001/XMLSchema#int":
			case "http://www.w3.org/2001/XMLSchema#short":
				return "INT";
			
			case "http://www.w3.org/2001/XMLSchema#long":
				return "BIGINT";
			
			
			case "http://www.w3.org/2001/XMLSchema#boolean":
			case "http://www.w3.org/2001/XMLSchema#byte":
			case "http://www.w3.org/2001/XMLSchema#unsignedByte":
				return "SMALLINT";
			
			case "http://www.w3.org/2001/XMLSchema#nonNegativeInteger":
			case "http://www.w3.org/2001/XMLSchema#unsignedInt":
			case "http://www.w3.org/2001/XMLSchema#unsignedShort":
			case "http://www.w3.org/2001/XMLSchema#positiveInteger":
				return "UNSIGNED INT";
			
			case "http://www.w3.org/2001/XMLSchema#unsignedLong":
				return "UNSIGNED BIGINT";
				
			case "http://www.w3.org/2001/XMLSchema#dateTime":
				return "DATETIME";
				
			case "http://www.w3.org/2001/XMLSchema#date":
				return "DATE";
			
			case "http://www.w3.org/2001/XMLSchema#time":
			case "http://www.w3.org/2001/XMLSchema#duration":
				return "TIME";

			case "http://www.w3.org/2001/XMLSchema#base64Binary":
				return "BLOB";

			case "http://www.w3.org/2001/XMLSchema#anyURI":
				// shouldn't be case-insensitive, but using BLOB
				// instead seems to make things too complex.
				return "TEXT";
		}
		
		return "TEXT";
	}
	
	public override void Init(VariableBinding[] variables) {
		output.Write("CREATE TABLE " + table + " (");
		
		bool f = true;
		foreach (VariableBinding var in variables) {
			if (var.Name == null) continue;
			
			string type = "BLOB";
			//if (var.Target is Literal && ((Literal)var.Target).DataType != null)
			//	type = GetFieldType(((Literal)var.Target).DataType);

			if (!f)  { output.Write(", "); } f = false; 
			output.Write(var.Name + " " + type);
		}
		
		output.WriteLine(");");
	}
	
	public override bool Add(VariableBinding[] result) {
		output.Write("INSERT INTO " + table + " VALUES (");
		bool firstx = true;
		foreach (VariableBinding var in result) {
			if (var.Name == null) continue;
			
			if (!firstx)  { output.Write(", "); } firstx = false;
			if (var.Target == null)
				output.Write("NULL");
			else if (var.Target is Literal)
				output.Write(Escape(((Literal)var.Target).Value));
			else if (var.Target.Uri != null)
				output.Write("\"" + var.Target.Uri + "\"");
			else
				output.Write("\"\"");
		}
		output.WriteLine(");");
		
		return true;
	}
	
	private string Escape(string str) {
		if (str == null) return "NULL";
		return "\"" + EscapeUnquoted(str) + "\"";
	}
	
	StringBuilder EscapeUnquotedBuffer = new StringBuilder();
	private string EscapeUnquoted(string str) {
		StringBuilder b = EscapeUnquotedBuffer;
		b.Length = 0;
		b.Append(str);
		Escape(b);
		return b.ToString();
	}
	
	internal static void Escape(StringBuilder b) {
		b.Replace("\\", "\\\\");
		b.Replace("\"", "\\\"");
		b.Replace("\n", "\\n");
		b.Replace("%", "\\%");
		b.Replace("*", "\\*");
	}

}

