using System;
using System.IO;

using SemWeb;
using SemWeb.Inference;

public class EulerTest {

	public static void Main(string[] args) {
		if (args.Length < 2) {
			Console.WriteLine("Usage: euler.exe axioms.n3 axioms... questions.n3");
			return;
		}
		
		// Load Axioms
		
		MemoryStore axioms = new MemoryStore();
		for (int i = 0; i < args.Length-1; i++) {
			N3Reader axiomsreader = new N3Reader(args[i]);
			axiomsreader.BaseUri = "http://www.example.org/arbitrary/base#";
			axioms.Import(axiomsreader);
		}
		
		Euler engine = new Euler(axioms);
		
		// Load question

		MemoryStore question = new MemoryStore();
		question.Import(new N3Reader(args[args.Length-1]));
		
		Proof[] proofs = engine.Prove(null, question.ToArray());
			
		foreach (Proof p in proofs) {
			Console.WriteLine(p.ToString());
			break; // just show one?
		}
		
	}

}
