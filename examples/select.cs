// This example reads an RDF/XML file on standard input
// and writes it back out statement-by-statement using
// each of the Select overloads.

using System;
using SemWeb;

public class Select {
	public static void Main() {
		MemoryStore store = new MemoryStore();
		
		store.Import(new RdfXmlReader(Console.In));
		
		foreach (Statement stmt in store.Select(Statement.All)) {
			Console.WriteLine(stmt);
		}
		Console.WriteLine();
		
		foreach (Statement stmt in store.Select(new Statement("http://www.example.com/myobject", null, null))) {
			Console.WriteLine(stmt);
		}
		Console.WriteLine();
		
		store.Select(Statement.All, new MyStatementWriter());
		Console.WriteLine();
	}
	
	class MyStatementWriter : StatementSink {
		public bool Add(Statement s) {
			Console.WriteLine(s.Subject);
			return true;
		}
	}
}
