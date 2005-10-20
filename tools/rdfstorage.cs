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

		[Mono.GetOptions.Option("Quiet mode: Don't emit status information.")]
		public bool quiet = false;

		[Mono.GetOptions.Option("Check for duplicate statements.")]
		public bool dupcheck = false;
	}
	
	public static void Main(string[] args) {
		try {
	
		Opts opts = new Opts();
		opts.ProcessArgs(args);

		if (opts.RemainingArguments.Length == 0 && opts.@out == "n3:-") {
			opts.DoHelp();
			return;
		}
		
		StatementSink storage = Store.CreateForOutput(opts.@out);
		
		Entity meta = null;
		if (opts.meta != null)
			meta = new Entity(opts.meta);
		
		if (opts.clear) {
			if (!(storage is Store)) {
				Console.Error.WriteLine("The --clear option cannot be used with this type of output storage.  Ignoring --clear.");
			} else {
				try {
					if (meta == null)
						((Store)storage).Clear();
					else
						((Store)storage).Remove(new Statement(null, null, null, meta));
				} catch (Exception e) {
					Console.Error.WriteLine("The --clear option was not successful: " + e.Message);
				}
			}
		}
		
		MultiRdfParser multiparser = new MultiRdfParser(opts.RemainingArguments, opts.@in, meta, opts.baseuri, opts.quiet, opts.dupcheck);
		
		if (storage is Store)
			((Store)storage).Import(multiparser);
		else
			multiparser.Select(storage);
		
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
		bool dupcheck;
		
		public MultiRdfParser(IList files, string format, Entity meta, string baseuri, bool quiet, bool dupcheck) {
			this.files = files;
			this.format = format;
			this.meta = meta;
			this.baseuri = baseuri;
			this.quiet = quiet;
			this.dupcheck = dupcheck;
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
						parser.DuplicateCheck = dupcheck;
						parser.Select(filter);
						parser.Dispose();
					} else {
						StatementSource src = Store.CreateForInput(infile);
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

