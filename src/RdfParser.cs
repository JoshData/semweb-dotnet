using System;
using System.Collections;
using System.IO;
 
namespace SemWeb {
	public class ParserException : ApplicationException {
		public ParserException (string message) : base (message) {}
	}

	public abstract class RdfParser : IDisposable {
		Entity meta = null;
		string baseuri = null;
		ArrayList warnings = new ArrayList();
		protected ArrayList variables = new ArrayList();
		
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
		
		public ICollection Variables { get { return ArrayList.ReadOnly(variables); } }

		public abstract void Parse(Store storage);
		
		public virtual void Dispose() {
		}
		
		public static RdfParser Create(string type, string source) {
			switch (type) {
				case "xml":
				case "text/xml":
					return new SemWeb.IO.RdfXmlParser(source);
				case "n3":
				case "text/n3":
					return new SemWeb.IO.N3Parser(source);
				default:
					throw new ArgumentException("Unknown parser type: " + type);
			}
		}
		
		internal static TextReader GetReader(string file) {
			if (file == "-") return Console.In;
			return new StreamReader(file);
		}
		
		protected void OnWarning(string message) {
			warnings.Add(message);
		}
	}
	
	internal class MultiRdfParser : RdfParser {
		private ArrayList parsers = new ArrayList();
		
		public ArrayList Parsers { get { return parsers; } }
		
		public override void Parse(Store storage) {
			foreach (RdfParser p in Parsers)
				p.Parse(storage);
		}
	}
}

