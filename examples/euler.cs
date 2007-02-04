// This example demonstrates general reasoning with
// the Euler engine based on Jos De Roo's Euler proof
// mechanism.  The example is based on the "graph"
// example from Euler.

using System;
using System.IO;

using SemWeb;
using SemWeb.Inference;

public class EulerTest {

	public static void Main() {
		// Create the instance data
		
		MemoryStore dataModel = new MemoryStore();
		
		BNode paris = new BNode("paris");
		BNode orleans = new BNode("orleans");
		BNode chartres = new BNode("chartres");
		BNode amiens = new BNode("amiens");
		BNode blois = new BNode("blois");
		BNode bourges = new BNode("bourges");
		BNode tours = new BNode("tours");
		BNode lemans = new BNode("lemans");
		BNode angers = new BNode("angers");
		BNode nantes = new BNode("nantes");
	
		Entity oneway = new Entity("http://www.agfa.com/w3c/euler/graph.axiom#oneway");
		Entity path = new Entity("http://www.agfa.com/w3c/euler/graph.axiom#path");
		
		dataModel.Add(new Statement(paris, oneway, orleans));
		dataModel.Add(new Statement(paris, oneway, chartres));
		dataModel.Add(new Statement(paris, oneway, amiens));
		dataModel.Add(new Statement(orleans, oneway, blois));
		dataModel.Add(new Statement(orleans, oneway, bourges));
		dataModel.Add(new Statement(blois, oneway, tours));
		dataModel.Add(new Statement(chartres, oneway, lemans));
		dataModel.Add(new Statement(lemans, oneway, angers));
		dataModel.Add(new Statement(lemans, oneway, tours));
		dataModel.Add(new Statement(angers, oneway, nantes));
		
		// Create the inference rules by reading them from a N3 string.
		
		string rules =
			"@prefix : <http://www.agfa.com/w3c/euler/graph.axiom#>.\n" +
			"\n" +
			"{ ?a :oneway ?b } => { ?a :path ?b } .\n" +
			"{ ?a :path ?b . ?b :path ?c . } => { ?a :path ?c } .\n";
		
		// Create our question in the form of a statement to test.
		
		Statement question = new Statement(paris, path, nantes);
		
		// Create the Euler engine
		
		Euler engine = new Euler(new N3Reader(new StringReader(rules)));
		
		// First Method of Inference:
		// Ask the engine whether there is a path from paris to nantes.
		// The Prove method will return a list of proofs, or an empty
		// array if it could not find a proof.
		
		foreach (Proof p in engine.Prove(dataModel, new Statement[] { question })) {
			Console.WriteLine(p.ToString());
		}
		
		// Second Method of Inference:
		// Apply the engine to the data model and then use the data
		// model's Contains method to see if the statement is "in"
		// the model + reasoning.
		
		dataModel.AddReasoner(engine);
		
		Console.WriteLine("Euler Says the Question is: " + dataModel.Contains(question));
		
	}

}
