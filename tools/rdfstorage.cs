using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;

using SemWeb;
//using SemWeb.Algos;

[assembly: AssemblyTitle("RDFStorage - Move RDF Data Between Storage Types")]
[assembly: AssemblyCopyright("Copyright (c) 2006 Joshua Tauberer <http://razor.occams.info>\nreleased under the GPL.")]
[assembly: AssemblyDescription("A tool to move RDF data between storage types.")]

[assembly: Mono.UsageComplement("file1 file2...")]

public class RDFStorage {
	private class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("The {format} for the input files: xml, n3, url to treat the command-line arguments as URLs (format determined automatically), or a specification string used with Store.Create. The default autodetects from the file name.")]
		public string @in = null;

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
		
		[Mono.GetOptions.Option("Read and write on separate threads buffering the given {number} of statements.")]
		public int buffer = 0;

		/*[Mono.GetOptions.Option("Make the output lean.")]
		public bool makelean = false;

		[Mono.GetOptions.Option("Make lean in comparison to another data source.")]
		public string leanagainst = null;
		
		[Mono.GetOptions.Option("Write out lean-removed statements.")]
		public bool leanprogress = false;*/
	}
	
	static long totalStatementsRead = 0;
	
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
		
		DateTime start_time = DateTime.Now;
		
		MultiRdfParser multiparser = new MultiRdfParser(opts.RemainingArguments, opts.@in, meta, opts.baseuri, !opts.stats);
		
		if (opts.buffer > 0) {
			CircularStatementBuffer buffer = new CircularStatementBuffer(opts.buffer);
			buffer.BeginReading(multiparser);
			buffer.BeginWriting(storage);
			buffer.Wait();
		} else if (storage is ModifiableSource) {
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

		TimeSpan alltime = DateTime.Now - start_time;
		if (opts.stats)
			Console.Error.WriteLine("Total Time: {0}m{1}s, {2} statements, {3} st/sec", (int)alltime.TotalMinutes, (int)alltime.Seconds, totalStatementsRead, alltime.TotalSeconds == 0 ? "?" : ((int)(totalStatementsRead/alltime.TotalSeconds)).ToString());
		
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
			foreach (string infile in files) {
				if (!quiet)
					Console.Error.Write(infile + " ");
				
				try {
					DateTime start = DateTime.Now;
				
					StatementFilterSink filter = new StatementFilterSink(storage);
				
					if (format == null || format != "spec") {
						string fmt = format;
						if (fmt == null) {
							// Use file extension to override default parser type.
							if (infile.StartsWith("http:"))
								fmt = "url";
							else if (infile.EndsWith(".nt") || infile.EndsWith(".n3") || infile.EndsWith(".ttl"))
								fmt = "n3";
							else if (infile.EndsWith(".xml") || infile.EndsWith(".rdf"))
								fmt = "xml";
							else {
								Console.Error.WriteLine("Unrecognized file extension in " + infile + ": Trying RDF/XML.");
								fmt = "xml";
							}
						}
					
						using (RdfReader parser = RdfReader.Create(fmt, infile)) {
							if (baseuri != null) parser.BaseUri = baseuri;
							if (meta != null) parser.Meta = meta;
						
							if (storage is RdfWriter)
								((RdfWriter)storage).Namespaces.AddFrom(parser.Namespaces);
						
							try {
								parser.Select(filter);
							} finally {
								if (parser.Warnings.Count > 0)
									Console.Error.WriteLine("\nThere were warnings parsing this file:");
								foreach (string warning in parser.Warnings)
									Console.Error.WriteLine("> " + warning);
							}
						}
					} else {
						StatementSource src = Store.Create(infile);
						src.Select(filter);
						if (src is IDisposable)
							((IDisposable)src).Dispose();
					}
					
					totalStatementsRead += filter.StatementCount;
					
					TimeSpan time = DateTime.Now - start;
					
					if (!quiet)
						Console.Error.WriteLine(" {0}m{1}s, {2} statements, {3} st/sec", (int)time.TotalMinutes, (int)time.Seconds, filter.StatementCount, time.TotalSeconds == 0 ? "?" : ((int)(filter.StatementCount/time.TotalSeconds)).ToString());
				} catch (ParserException e) {
					Console.Error.WriteLine(" " + e.Message);
				} catch (Exception e) {
					Console.Error.WriteLine("\n" + e + "\n");
				}
			}
			
		}
	}
}

internal class StatementFilterSink : StatementSink, CanForgetBNodes {
	StatementSink sink;
	int counter = 0;
	
	public int StatementCount { get { return counter; } }
	
	public StatementFilterSink(StatementSink sink) { this.sink = sink; }
	
	public bool Add(Statement statement) {
		counter++;
		sink.Add(statement);
		return true;
	}
	
	public void ForgetBNode(BNode node) {
		CanForgetBNodes x = sink as CanForgetBNodes;
		if (x != null) x.ForgetBNode(node);
	}
}

internal class CircularStatementBuffer : StatementSource, StatementSink {
	const int SLEEP_DURATION = 0;

	Statement[] buffer;
	int len;
	volatile int nextWrite = 0;
	volatile int nextRead = 0;
	volatile bool finished = false;
	volatile bool canceled = false;

	AutoResetEvent hasData = new AutoResetEvent(false);
	AutoResetEvent hasSpace = new AutoResetEvent(false);
	
	StatementSource sourceData;
	StatementSink targetSink;
	Thread writer, reader;

	public CircularStatementBuffer(int size) {
		len = size;
		buffer = new Statement[size];
	}
	
	public void BeginWriting(StatementSink sink) {
		targetSink = sink;
		reader = new Thread(ReaderRunner);
		reader.Start();
	}
	
	public void BeginReading(StatementSource source) {
		sourceData = source;
		writer = new Thread(WriterRunner);
		writer.Start();
	}
	
	public void Wait() {
		writer.Join();
		reader.Join();
	}

	void WriterRunner() {
		sourceData.Select(this);
		finished = true;
		hasData.Set(); // anything written but not flagged
	}
	
	public bool Add(Statement statement) {
		// Check that we can advance (i.e. not fill up the buffer
		// so that our pointers cross).
		
		int nw = nextWrite;
		int next = (nw == len-1) ? 0 : nw+1;
		
		while (next == nextRead && !canceled) {
			if (SLEEP_DURATION > 0)
				Thread.Sleep(SLEEP_DURATION);
			else
				hasSpace.WaitOne();
		}
		
		if (canceled) return false;
		
		buffer[nw] = statement;
		nextWrite = next;
		
		if ((nw & 0xFF) == 0)
			hasData.Set();
		
		return true;
	}
	
	void ReaderRunner() {
		if (targetSink is ModifiableSource)
			((ModifiableSource)targetSink).Import(this);
		else
			ReadLoop(targetSink);
	}
	
	bool StatementSource.Distinct { get { return false; } }
	
	void StatementSource.Select(StatementSink sink) {
		ReadLoop(sink);
	}
	
	void ReadLoop(StatementSink sink) {
		while (!finished) {
			int nr = nextRead;

			// Check that we can advance (i.e. not cross the write pointer).
			while (nr == nextWrite && !finished) {
				if (SLEEP_DURATION > 0)
					Thread.Sleep(SLEEP_DURATION);
				else
					hasData.WaitOne();
			}

			if (finished) return;
			
			int nw = nextWrite;
			int addctr = 0;
		
			while (nr != nw) {
				Statement s = buffer[nr];
				nr = (nr == len-1) ? 0 : nr+1;

				if ((addctr++ & 0xFF) == 0) {
					nextRead = nr;
					hasSpace.Set();
				}

				canceled = !sink.Add(s);
				if (canceled) break;
			}
			
			nextRead = nr;
			hasSpace.Set();
			if (canceled) break;
		}
	}
	
}
