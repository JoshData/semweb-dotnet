using System;
using SemWeb;

public class Sqlite {
	const string RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
	const string FOAF = "http://xmlns.com/foaf/0.1/";
	
	static readonly Entity rdftype = RDF+"type";
	static readonly Entity foafPerson = FOAF+"Person";
	static readonly Entity foafname = FOAF+"name";

	public static void Main() {
		Store store = Store.Create("sqlite:rdf:Uri=file:foaf.sqlite;version=3");
		
		Entity newPerson = new Entity("http://www.example.org/me");
		store.Add(new Statement(newPerson, rdftype, foafPerson));
		store.Add(new Statement(newPerson, foafname, (Literal)"New Guy"));
		
		Console.WriteLine("These are the people in the file:");
		foreach (Entity s in store.SelectSubjects(rdftype, foafPerson)) {
			foreach (Resource r in store.SelectObjects(s, foafname))
				Console.WriteLine(r);
		}
		Console.WriteLine();
		
		store.Dispose();
	}
}
