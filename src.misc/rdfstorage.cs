using System;
using System.IO;
using System.Reflection;

using SemWeb;

[assembly: AssemblyTitle("RDFStorage - Move RDF Data Between Storage Types")]
[assembly: AssemblyCopyright("Copyright (c) 2005 Joshua Tauberer <tauberer@for.net>\nreleased under the GPL.")]
[assembly: AssemblyDescription("A tool to move RDF data between storage types.")]

[assembly: Mono.UsageComplement("file1 file2...")]

public class RDFStorage {
	private class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("The {format} for the input files: xml or n3.")]
		public string @in = "xml";

		[Mono.GetOptions.Option("The destination storage.")]
		public string store = null;

		[Mono.GetOptions.Option("Clears the storage before importing data.")]
		public bool clear = false;
	}
	
	public static void Main(string[] args) {
		Opts opts = new Opts();
		opts.ProcessArgs(args);

		if (opts.RemainingArguments.Length == 0 || opts.store == null) {
			opts.DoHelp();
			return;
		}
		
		KnowledgeModel model = new KnowledgeModel();
		Store storage = Store.CreateForOutput(opts.store, model);
		model.Add(storage);
		
		if (opts.clear)
			storage.Clear();
		
		foreach (string infile in opts.RemainingArguments) {
			Console.Error.WriteLine(infile);
			RdfParser parser = RdfParser.Create(opts.@in, infile);
			storage.Import(parser);
		}
	}
}

