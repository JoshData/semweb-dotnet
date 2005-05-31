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
		try {
	
		Opts opts = new Opts();
		opts.ProcessArgs(args);

		if (opts.RemainingArguments.Length == 0 && opts.@out == "n3:-") {
			opts.DoHelp();
			return;
		}
		
		StatementSinkEx storage = Store.CreateForOutput(opts.@out);
		
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
		
		MyMultiRdfParser multiparser = new MyMultiRdfParser(opts.RemainingArguments, opts.@in, meta);
		storage.Import(multiparser);
		if (storage is IDisposable) ((IDisposable)storage).Dispose();
		
		} catch (Exception exc) {
			Console.Error.WriteLine(exc);
		}
	}

	private class MyMultiRdfParser : RdfReader {
		IList files;
		string format;
		Entity meta;
		
		public MyMultiRdfParser(IList files, string format, Entity meta) {
			this.files = files;
			this.format = format;
			this.meta = meta;
		}
		
		public override void Parse(StatementSinkEx storage) {
			DateTime allstart = DateTime.Now;
			long stct = 0;
					
			foreach (string infile in files) {
				Console.Error.Write(infile);
				
				try {
					DateTime start = DateTime.Now;
				
					StatementFilterSink filter = new StatementFilterSink(storage);
				
					RdfReader parser = RdfReader.Create(format, infile);
					parser.Meta = meta;				
					parser.Parse(filter);
					parser.Dispose();
					
					stct += filter.StatementCount;
					
					TimeSpan time = DateTime.Now - start;
					
					Console.Error.WriteLine(" {0}m{1}s, {2} statements, {3} st/sec", (int)time.TotalMinutes, (int)time.Seconds, filter.StatementCount, time.TotalSeconds == 0 ? "?" : ((int)(filter.StatementCount/time.TotalSeconds)).ToString());
				} catch (ParserException e) {
					Console.Error.WriteLine(e.Message);
				} catch (Exception e) {
					Console.Error.WriteLine(e);
				}
			}
			
			TimeSpan alltime = DateTime.Now - allstart;
			Console.Error.WriteLine("Total Time: {0}m{1}s, {2} statements, {3} st/sec", (int)alltime.TotalMinutes, (int)alltime.Seconds, stct, alltime.TotalSeconds == 0 ? "?" : ((int)(stct/alltime.TotalSeconds)).ToString());
		}
	}
}

internal class StatementFilterSink : StatementSinkEx {
	StatementSink sink;
	int counter = 0;
	
	public int StatementCount { get { return counter; } }
	
	public StatementFilterSink(StatementSink sink) { this.sink = sink; }
	
	public bool Add(Statement statement) {
		counter++;
		sink.Add(statement);
		return true;
	}
	
	public Entity CreateAnonymousEntity() {
		if (!(sink is StatementSinkEx)) throw new InvalidOperationException();
		return ((StatementSinkEx)sink).CreateAnonymousEntity();
	}
	
	public void Import(RdfReader parser) {
		if (!(sink is StatementSinkEx)) throw new InvalidOperationException();
		((StatementSinkEx)sink).Import(parser);
	}
}

