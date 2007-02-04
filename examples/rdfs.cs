// This example demonstrates basic RDFS reasoning.

using System;
using System.IO;

using SemWeb;
using SemWeb.Inference;

public class EulerTest {

	public static void Main() {
		// Create the instance data
		
		MemoryStore dataModel = new MemoryStore();
		
		BNode me = new BNode("me");
		BNode you = new BNode("you");
		
		Entity rdfType = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
		Entity rdfsLabel= "http://www.w3.org/2000/01/rdf-schema#label";
		Entity foafPerson = "http://xmlns.com/foaf/0.1/Person";
		Entity foafAgent = "http://xmlns.com/foaf/0.1/Agent";
		Entity foafName = "http://xmlns.com/foaf/0.1/name";
		
		dataModel.Add(new Statement(me, rdfType, foafPerson));
		dataModel.Add(new Statement(you, rdfType, foafPerson));
		dataModel.Add(new Statement(me, foafName, (Literal)"John Doe"));
		dataModel.Add(new Statement(you, foafName, (Literal)"Sam Smith"));
		
		// Create the RDFS engine and apply it to the data model.
		
		RDFS engine = new RDFS();
		engine.LoadSchema(RdfReader.LoadFromUri(new Uri("http://xmlns.com/foaf/0.1/index.rdf")));
		
		dataModel.AddReasoner(engine);
		
		// Query the data model
		
		// Ask for who are typed as Agents.  Note that the people are
		// typed as foaf:Person, and the schema asserts that foaf:Person
		// is a subclass of foaf:Agent.
		Console.WriteLine("Who are Agents?");
		foreach (Entity r in dataModel.SelectSubjects(rdfType, foafAgent))
			Console.WriteLine("\t" + r);
		
		// Ask for the rdfs:labels of everyone.  Note that the data model
		// has foaf:names for the people, and the schema asserts that
		// foaf:name is a subproperty of rdfs:label.
		Console.WriteLine("People's labels:");
		foreach (Statement s in dataModel.Select(new Statement(null, rdfsLabel, null)))
			Console.WriteLine("\t" + s);
	}

}
