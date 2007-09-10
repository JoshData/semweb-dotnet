// This example loads an RDF file from the web.
// It uses RdfReader.LoadFromUri, passing a URI,
// which downloads the corresponding URI and looks
// at its MIME type to determine what kind of
// RdfReader should be used to parse the file. A
// default URI (http://www.mozilla.org/news.rdf)
// can be used, or pass a URI on the command line
// to use a different data source.

using System;
using SemWeb;

public class LoadFromWeb {
	public static void Main(string[] args) {
		string uri = "http://www.mozilla.org/news.rdf";
		if (args.Length > 0)
			uri = args[0];
	
		MemoryStore store = new MemoryStore();
		
		// Here's one way...

		store.Import(RdfReader.LoadFromUri(new Uri(uri)));

		using (RdfWriter writer = new N3Writer(Console.Out))
			store.Select(writer);
			
		// Or....

		RdfReader file = RdfReader.LoadFromUri(new Uri(uri));
		file.Select(new StatementPrinter());
	}
	
	class StatementPrinter : StatementSink {
		public bool Add(Statement assertion) {
			Console.WriteLine(assertion.ToString());
			return true;
		}
	}
}
