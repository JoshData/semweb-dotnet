using System;
using System.Collections;
using System.IO;
using System.Reflection;

using SemWeb;
//using SemWeb.Algos;

[assembly: AssemblyTitle("RDFStorage - Move RDF Data Between Storage Types")]
[assembly: AssemblyCopyright("Copyright (c) 2006 Joshua Tauberer <http://razor.occams.info>\nreleased under the GPL.")]
[assembly: AssemblyDescription("A tool to move RDF data between storage types.")]

[assembly: Mono.UsageComplement("file1 file2...")]

public class RDFStorage {
	private class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("The {format} for the input files: xml, n3, or spec to use a full spec.")]
		public string @in = "xml";

		[Mono.GetOptions.Option("The destination {storage}.  Default is N3 to standard out.")]
		public string @out = "n3:-";

		[Mono.GetOptions.Option("Clear the storage before importing data.")]
		public bool clear = false;

		[Mono.GetOptions.Option("The {URI} of a resource that expresses meta information.")]
		public string meta = null;
		
		[Mono.GetOptions.Option("The default base {URI} for the input streams.")]
		public string baseuri = null;

		[Mono.GetOptions.Option("The base {URI} for the output stream (if supported).")]
		public string outbaseuri = null;

		[Mono.GetOptions.Option("Emit status information to STDERR when writing to STDOUT.")]
		public bool stats = false;

		/*[Mono.GetOptions.Option("Make the output lean.")]
		public bool makelean = false;

		[Mono.GetOptions.Option("Make lean in comparison to another data source.")]
		public string leanagainst = null;
		
		[Mono.GetOptions.Option("Write out lean-removed statements.")]
		public bool leanprogress = false;*/
	}
	
	public static void Main(string[] args) {
		try {
	
		Opts opts = new Opts();
		opts.ProcessArgs(args);

		if (opts.RemainingArguments.Length == 0 && opts.@out == "n3:-") {
			opts.DoHelp();
			return;
		}
		
		if (!(opts.@out == "xml:-" || opts.@out == "n3:-"))
			opts.stats = true;
		
		StatementSink storage = Store.CreateForOutput(opts.@out);
		if (storage is RdfWriter && opts.outbaseuri != null)
			((RdfWriter)storage).BaseUri = opts.outbaseuri;
		
		Entity meta = null;
		if (opts.meta != null)
			meta = new Entity(opts.meta);
		
		if (opts.clear) {
			if (!(storage is ModifiableSource)) {
				Console.Error.WriteLine("The --clear option cannot be used with this type of output method.  Ignoring --clear.");
			} else {
				try {
					if (meta == null)
						((ModifiableSource)storage).Clear();
					else
						((ModifiableSource)storage).Remove(new Statement(null, null, null, meta));
				} catch (Exception e) {
					Console.Error.WriteLine("The --clear option was not successful: " + e.Message);
				}
			}
		}
		
		MultiRdfParser multiparser = new MultiRdfParser(opts.RemainingArguments, opts.@in, meta, opts.baseuri, !opts.stats);
		
		if (storage is ModifiableSource) {
			((ModifiableSource)storage).Import(multiparser);
		} else {
			//if (!opts.makelean) {
				multiparser.Select(storage);
			/*} else {
				MemoryStore st = new MemoryStore(multiparser);
				StatementSink removed = null;
				if (opts.leanprogress)
					removed = new N3Writer(Console.Out);
				if (opts.leanagainst != null) {
					StatementSource against = Store.CreateForInput(opts.leanagainst);
					if (!(against is SelectableSource))
						against = new MemoryStore(against);
					Lean.MakeLean(st, (SelectableSource)against, removed);
				} else if (storage is SelectableSource) {
					Lean.MakeLean(st, (SelectableSource)storage, removed);
				} else {
					Lean.MakeLean(st, null, removed);
				}
				st.Select(storage);
			}*/
		}
		
		if (storage is IDisposable) ((IDisposable)storage).Dispose();
		
		} catch (Exception exc) {
			Console.Error.WriteLine(exc);
		}
	}

	private class MultiRdfParser : RdfReader {
		IList files;
		string format;
		Entity meta;
		string baseuri;
		bool quiet;
		
		public MultiRdfParser(IList files, string format, Entity meta, string baseuri, bool quiet) {
			this.files = files;
			this.format = format;
			this.meta = meta;
			this.baseuri = baseuri;
			this.quiet = quiet;
		}
		
		public override void Select(StatementSink storage) {
			DateTime allstart = DateTime.Now;
			long stct = 0;
					
			foreach (string infile in files) {
				if (!quiet)
					Console.Error.Write(infile + " ");
				
				try {
					DateTime start = DateTime.Now;
				
					StatementFilterSink filter = new StatementFilterSink(storage);
				
					if (format != "spec") {
						RdfReader parser = RdfReader.Create(format, infile);
						parser.BaseUri = baseuri;
						if (meta != null) parser.Meta = meta;
						
						if (storage is RdfWriter)
							((RdfWriter)storage).Namespaces.AddFrom(parser.Namespaces);
						
						parser.Select(filter);
						foreach (string warning in parser.Warnings)
							Console.Error.WriteLine(warning);
						parser.Dispose();
					} else {
						StatementSource src = Store.Create(infile);
						src.Select(filter);
					}
					
					stct += filter.StatementCount;
					
					TimeSpan time = DateTime.Now - start;
					
					if (!quiet)
						Console.Error.WriteLine(" {0}m{1}s, {2} statements, {3} st/sec", (int)time.TotalMinutes, (int)time.Seconds, filter.StatementCount, time.TotalSeconds == 0 ? "?" : ((int)(filter.StatementCount/time.TotalSeconds)).ToString());
				} catch (ParserException e) {
					Console.Error.WriteLine(" " + e.Message);
				} catch (Exception e) {
					Console.Error.WriteLine("\n" + e + "\n");
				}
			}
			
			TimeSpan alltime = DateTime.Now - allstart;
			if (!quiet)
				Console.Error.WriteLine("Total Time: {0}m{1}s, {2} statements, {3} st/sec", (int)alltime.TotalMinutes, (int)alltime.Seconds, stct, alltime.TotalSeconds == 0 ? "?" : ((int)(stct/alltime.TotalSeconds)).ToString());
		}
	}
}

internal class StatementFilterSink : StatementSink {
	StatementSink sink;
	int counter = 0;
	
	public int StatementCount { get { return counter; } }
	
	public StatementFilterSink(StatementSink sink) { this.sink = sink; }
	
	public bool Add(Statement statement) {
		counter++;
		sink.Add(statement);
		return true;
	}
}

