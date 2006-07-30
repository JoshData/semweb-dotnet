// This example loads an RDF file from the web.

using System;
using SemWeb;

public class LoadFromWEb {
	public static void Main() {
		MemoryStore store = new MemoryStore();
		//store.Import(RdfReader.LoadFromUri(new Uri("http://www.mozilla.org/news.rdf")));
		using (RdfWriter writer = new N3Writer(Console.Out))
			store.StreamTo(writer);

		RdfReader file = RdfReader.LoadFromUri(new Uri("http://www.mozilla.org/news.rdf"));
		file.StreamTo(new StatementPrinter());
	}
	
	class StatementPrinter : StatementSink {
		public bool Add(Statement assertion) {
			Console.WriteLine(assertion.ToString());
			return true;
		}
	}
}
