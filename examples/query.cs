// This example runs a query.

using System;
using System.IO;

using SemWeb;
using SemWeb.Query;

public class Example {

	public static void Main(string[] argv) {
		if (argv.Length < 3) {
			Console.WriteLine("Usage: query.exe format queryfile datafile");
			return;
		}
		
		string format = argv[0];
		string queryfile = argv[1];
		string datafile = argv[2];
	
		Query query;
		
		if (format == "rsquary") {
			// Create a simple-entailment "RSquary" query
			// from the N3 file.
			query = new GraphMatch(new N3Reader(queryfile));
		} else {
			// Create a SPARQL query by reading the file's
			// contents.
			query = new Sparql(new StreamReader(queryfile));
		}
	
		// Load the data file from disk
		MemoryStore data = new MemoryStore();
		data.Import(new N3Reader(datafile));
		
		// Create a result sink where results are written to.
		QueryResultSink sink = new SparqlXmlQuerySink(Console.Out);
		
		// Run the query.
		query.Run(data, sink);
	}
}
