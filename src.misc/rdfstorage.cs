using System;
using System.Collections;
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

		[Mono.GetOptions.Option("The destination storage.  Default is N3 to standard out.")]
		public string @out = "n3:-";

		[Mono.GetOptions.Option("Clear the storage before importing data.")]
		public bool clear = false;

		[Mono.GetOptions.Option("The URI of a resource that expresses meta information.")]
		public string meta = null;
	}
	
	public static void Main(string[] args) {
		Opts opts = new Opts();
		opts.ProcessArgs(args);

		if (opts.RemainingArguments.Length == 0) {
			opts.DoHelp();
			return;
		}
		
		KnowledgeModel model = new KnowledgeModel();
		Store storage = Store.CreateForOutput(opts.@out, model);
		model.Add(storage);
		
		if (opts.clear)
			storage.Clear();
		
		Entity meta = null;
		if (opts.meta != null)
			meta = model.GetResource(opts.meta);
		
		MyMultiRdfParser multiparser = new MyMultiRdfParser(opts.RemainingArguments, opts.@in, meta);
		storage.Import(multiparser);
		if (storage is IDisposable) ((IDisposable)storage).Dispose();
	}

	private class MyMultiRdfParser : RdfParser {
		IList files;
		string format;
		Entity meta;
		
		public MyMultiRdfParser(IList files, string format, Entity meta) {
			this.files = files;
			this.format = format;
			this.meta = meta;
		}
		
		public override void Parse(Store storage) {
			foreach (string infile in files) {
				Console.Error.WriteLine(infile);
				
				try {
					RdfParser parser = RdfParser.Create(format, infile);
					parser.Meta = meta;				
					parser.Parse(storage);					
					parser.Dispose();
				} catch (ParserException e) {
					Console.Error.WriteLine(e.Message);
				} catch (Exception e) {
					Console.Error.WriteLine(e);
				}
			}
		}
	}
}

