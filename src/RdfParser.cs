using System;
using System.Collections;
using System.IO;
 
namespace SemWeb {
	public class ParserException : ApplicationException {
		public ParserException (string message) : base (message) {}
	}

	public abstract class RdfReader : IDisposable {
		Entity meta = null;
		string baseuri = null;
		ArrayList warnings = new ArrayList();
		ArrayList variables = new ArrayList();
		
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
		
		protected void AddVariable(Entity variable) {
			variables.Add(variable);
		}

		public abstract void Parse(StatementSinkEx storage);
		
		public virtual void Dispose() {
		}
		
		public static RdfReader Create(string type, string source) {
			switch (type) {
				case "xml":
				case "text/xml":
					return new SemWeb.IO.RdfXmlReader(source);
				case "n3":
				case "text/n3":
					return new SemWeb.IO.N3Reader(source);
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
	
	internal class MultiRdfReader : RdfReader {
		private ArrayList parsers = new ArrayList();
		
		public ArrayList Parsers { get { return parsers; } }
		
		public override void Parse(StatementSinkEx storage) {
			foreach (RdfReader p in Parsers)
				p.Parse(storage);
		}
	}
}

