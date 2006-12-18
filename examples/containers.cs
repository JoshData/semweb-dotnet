// This example deals with RDF containers.  You can use the rdfs:member
// property to match any rdf:_### (i.e. rdf:li) property.

using System;
using SemWeb;

public class Containers {

	const string RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
	const string RDFS = "http://www.w3.org/2000/01/rdf-schema#";
	
	public static void Main() {
		MemoryStore store = new MemoryStore();
		
		Entity container = new Entity("http://www.example.org/#container");
		
		store.Add(new Statement(container, RDF+"type", (Entity)(RDF+"Bag")));
		store.Add(new Statement(container, RDF+"_1", (Literal)"One"));
		store.Add(new Statement(container, RDF+"_2", (Literal)"Two"));
		store.Add(new Statement(container, RDF+"_3", (Literal)"Three"));
		
		// use the rdfs:member property to match for any rdf:_### predicates.
		Entity rdfs_member = (Entity)(RDFS+"member");
		
		using (RdfWriter writer = new N3Writer(Console.Out)) {
			writer.Namespaces.AddNamespace(RDF, "rdf");
			
			store.Select(new Statement(container, rdfs_member, null), writer);
		}
	}
}
