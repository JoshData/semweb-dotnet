using System;
using System.IO;

using SemWeb;
using SemWeb.Inference;
using SemWeb.Query;

public class EulerDriver {

	public static void Main(string[] args) {
		if (args.Length < 2) {
			Console.WriteLine("Usage: euler.exe axioms.n3 axioms... {questions.n3 | -sparql query.sparql}");
			return;
		}
		
		// Load Axioms
		
		bool sparql = false;
		
		MemoryStore axioms = new MemoryStore();
		for (int i = 0; i < args.Length-1; i++) {
			if (i > 0 && i == args.Length-2 && args[i] == "-sparql") {
				sparql = true;
				break;
			}
			
			N3Reader axiomsreader = new N3Reader(args[i]);
			axiomsreader.BaseUri = "http://www.example.org/arbitrary/base#";
			axioms.Import(axiomsreader);
		}
		
		Euler engine = new Euler(axioms);
		
		// Load question
		if (!sparql) {
			MemoryStore question = new MemoryStore();
			question.Import(new N3Reader(args[args.Length-1]));
		
			Proof[] proofs = engine.Prove(null, question.ToArray());
			
			foreach (Proof p in proofs) {
				Console.WriteLine(p.ToString());
			}
		} else {
			using (StreamReader fs = new StreamReader(args[args.Length-1])) {
				string q = fs.ReadToEnd();
				
				Store store = new Store();
				store.AddReasoner(engine);
				
				SparqlEngine s = new SparqlEngine(q);
				s.Run(store, Console.Out);
			}
		}
	}

}
