using System;
using System.IO;

using SemWeb;
using SemWeb.Inference;

public class EulerTest {

	public static void Main(string[] args) {
		if (args.Length < 2) {
			Console.WriteLine("Usage: euler.exe axioms.n3 questions.n3");
			return;
		}
		
		N3Reader reader = new N3Reader(args[0]);
		reader.BaseUri = "http://www.example.org/arbitrary/base#";

		MemoryStore axioms = new MemoryStore();
		axioms.Import(reader);
		
		Euler engine = new Euler(axioms);

		MemoryStore question = new MemoryStore();
		question.Import(new N3Reader(args[1]));
		
		Proof[] proofs = engine.Prove(null, question.ToArray());
			
		foreach (Proof p in proofs) {
			Console.WriteLine(p.ToString());
			break; // just show one?
		}
		
	}

}
