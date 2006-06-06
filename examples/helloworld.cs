// This example creates a few RDF statements and adds
// them to a MemoryStore.  Then it writes out the
// statements in RDF/XML format to the console.  Note
// that the implicit string-to-Entity and string-to-
// Literal conversion operators are being used.

using System;
using SemWeb;

public class Example {

	const string RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

	public static void Main() {
		MemoryStore store = new MemoryStore();
		
		Entity computer = new Entity("http://example.org/computer");
		Entity says = "http://example.org/says";
		Entity wants = "http://example.org/wants";
		Entity desire = new BNode();
		Entity description = new Entity("http://example.org/description");
		
		store.Add(new Statement(computer, says, (Literal)"Hello world!"));
		store.Add(new Statement(computer, wants, desire));
		store.Add(new Statement(desire, description, (Literal)"to be human"));
		store.Add(new Statement(desire, RDF+"type", (Entity)"http://example.org/Desire"));
		
		using (RdfWriter writer = new RdfXmlWriter(Console.Out)) {
			writer.Namespaces.AddNamespace("http://example.org/", "ex");
			writer.Write(store);
		}
	}
}
