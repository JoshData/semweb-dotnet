
using System;
using SemWeb;

public class Simple {
	const string RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
	const string FOAF = "http://xmlns.com/foaf/0.1/";
	
	static readonly Entity rdftype = RDF+"type";
	static readonly Entity foafPerson = FOAF+"Person";
	static readonly Entity foafknows = FOAF+"knows";
	static readonly Entity foafname = FOAF+"name";

	public static void Main() {
		Store store = new MemoryStore();
		store.Import(new N3Reader("rdfsample.n3"));
		
		Console.WriteLine("These are the people in the file:");
		foreach (Statement s in store.Select(new Statement(null, rdftype, foafPerson))) {
			Console.WriteLine(s.Subject.Uri);
		}
		Console.WriteLine();

		Console.WriteLine("These are the knows relations in the file:");
		foreach (Statement s in store.Select(new Statement(null, foafknows, null))) {
			Console.WriteLine(s.Subject.Uri + " knows " + s.Object.Uri);
		}
		Console.WriteLine();
		
		Console.WriteLine("And here's RDF/XML just for some of the file:");
		using (RdfWriter w = new RdfXmlWriter(Console.Out)) {
			store.Select(new Statement(null, foafname, null), w);
			store.Select(new Statement(null, foafknows, null), w);
		}
		Console.WriteLine();
		
	}
}
