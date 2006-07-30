using System;
using System.IO;

using SemWeb;
using SemWeb.Query;

public class SparqlTest {

	public static void Main(string[] args) {
		if (args.Length != 2) {
			Console.Error.WriteLine("Usage: sparql.exe xml:datafile.rdf queryfile.sparql");
			return;
		}
		
		StatementSource source1 = Store.CreateForInput(args[0]);
		SelectableSource source = source1 as SelectableSource;
		if (source == null) source = new MemoryStore(source1);
		
		Sparql sparql = new Sparql(new StreamReader(args[1]));
		
		sparql.Run(source, Console.Out);
	}

}