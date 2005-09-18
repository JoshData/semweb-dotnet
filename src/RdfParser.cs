using System;
using System.Collections;
using System.IO;
using System.Web;
 
namespace SemWeb {
	public class ParserException : ApplicationException {
		public ParserException (string message) : base (message) {}
		public ParserException (string message, Exception cause) : base (message, cause) {}
	}

	public abstract class RdfReader : StatementSource, IDisposable {
		Entity meta = Statement.DefaultMeta;
		string baseuri = null;
		ArrayList warnings = new ArrayList();
		Hashtable variablenames = new Hashtable();
		bool reuseentities = false;
		bool dupcheck = false;

		public Entity Meta {
			get {
				return meta;
			}
			set {
				meta = value;
			}
		}
		
		public string BaseUri {
			get {
				return baseuri;
			}
			set {
				baseuri = value;
			}
		}
		
		public bool ReuseEntities {
			get {
				return reuseentities;
			}
			set {
				reuseentities = value;
			}
		}
		
		public bool DuplicateCheck {
			get {
				return dupcheck;
			}
			set {
				dupcheck = value;
			}
		}
		
		public IDictionary Variables { get { return (Hashtable)variablenames.Clone(); } }
		
		protected void AddVariableName(Entity variable, string name) {
			variablenames[variable] = name;
		}

		public abstract void Select(StatementSink sink);
		
		public virtual void Dispose() {
		}
		
		public static RdfReader Create(string type, string source) {
			switch (type) {
				case "xml":
				case "text/xml":
					return new RdfXmlReader(source);
				case "n3":
				case "text/n3":
					return new N3Reader(source);
				default:
					throw new ArgumentException("Unknown parser type: " + type);
			}
		}
		
		public static RdfReader LoadFromUri(Uri webresource) {
			System.Net.WebRequest rq = System.Net.WebRequest.Create(webresource);
			System.Net.WebResponse resp = rq.GetResponse();
			
			string mimetype = resp.ContentType;
			if (mimetype.IndexOf(';') > -1)
				mimetype = mimetype.Substring(0, mimetype.IndexOf(';'));
			
			switch (mimetype.Trim()) {
				case "text/xml":
				case "application/xml":
				case "application/rss+xml":
				case "application/rdf+xml":
					return new RdfXmlReader(resp.GetResponseStream());
					
				case "text/rdf+n3":
				case "application/n3":
				case "application/turtle":
				case "application/x-turtle":
					return new N3Reader(new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8));
			}
			
			if (webresource.LocalPath.EndsWith(".xml") || webresource.LocalPath.EndsWith(".rss"))
				return new RdfXmlReader(resp.GetResponseStream());
			
			if (webresource.LocalPath.EndsWith(".n3") || webresource.LocalPath.EndsWith(".ttl") || webresource.LocalPath.EndsWith(".nt"))
				return new N3Reader(new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8));

			throw new InvalidOperationException("Could not determine the RDF format of the resource.");
		}
		
		internal static TextReader GetReader(string file) {
			if (file == "-") return Console.In;
			return new StreamReader(file);
		}
		
		protected void OnWarning(string message) {
			warnings.Add(message);
		}
		
		internal string GetAbsoluteUri(string baseuri, string uri) {
			if (baseuri == null) return uri;
			if (uri.IndexOf(':') != -1) return uri;
			try {
				UriBuilder b = new UriBuilder(baseuri);
				b.Fragment = null; // per W3 RDF/XML test suite
				return new Uri(b.Uri, uri, true).ToString();
			} catch (UriFormatException e) {
				return baseuri + uri;
			}			
		}
		
		internal StatementSink GetDupCheckSink(StatementSink sink) {
			if (!dupcheck) return sink;
			if (!(sink is Store)) return sink;
			return new DupCheckSink((Store)sink);
		}
		
		private class DupCheckSink : StatementSink {
			Store store;
			public DupCheckSink(Store store) { this.store = store; }
			public bool Add(Statement s) {
				if (store.Contains(s)) return true;
				store.Add(s);
				return true;
			}
		}
	}
	
	internal class MultiRdfReader : RdfReader {
		private ArrayList parsers = new ArrayList();
		
		public ArrayList Parsers { get { return parsers; } }
		
		public override void Select(StatementSink storage) {
			foreach (RdfReader p in Parsers)
				p.Select(storage);
		}
	}
}

