using System;
using System.IO;
using System.Xml;

namespace SemWeb {
	public class RdfXmlWriter : RdfWriter {
		XmlWriter writer;
		NamespaceManager ns;
		NamespaceManager autons;
		
		long anonCounter = 0;
		string currentSubject = null;
		
		string rdf;
		
		public RdfXmlWriter(string file) : this(file, null) { }
		
		public RdfXmlWriter(string file, NamespaceManager ns) : this(GetWriter(file), ns) { }

		public RdfXmlWriter(TextWriter writer) : this(writer, null) { }
		
		public RdfXmlWriter(TextWriter writer, NamespaceManager ns) : this(NewWriter(writer), ns) { }
		
		private static XmlWriter NewWriter(TextWriter writer) {
			XmlTextWriter ret = new XmlTextWriter(writer);
			ret.Formatting = Formatting.Indented;
			ret.Indentation = 1;
			ret.IndentChar = '\t';
			return ret;
		}
		
		public RdfXmlWriter(XmlWriter writer) : this(writer, null) { }
		
		public RdfXmlWriter(XmlWriter writer, NamespaceManager ns) {
			if (ns == null)
				ns = new NamespaceManager();
			this.writer = writer;
			this.ns = ns;
			autons = new AutoPrefixNamespaceManager(this.ns);
		}
		
		public override NamespaceManager Namespaces { get { return ns; } }
		
		private bool Open(string resource, string type) {
			bool emittedType = false;
			
			if (currentSubject == null) {
				rdf = autons.GetPrefix("http://www.w3.org/1999/02/22-rdf-syntax-ns#");
				writer.WriteStartElement(rdf + ":RDF");
				foreach (string prefix in autons.GetPrefixes())
					writer.WriteAttributeString("xmlns:" + prefix, autons.GetNamespace(prefix));
			}
			if (currentSubject != null && currentSubject != resource)
				writer.WriteEndElement();
			if (currentSubject == null || currentSubject != resource) {
				currentSubject = resource;
				
				if (type == null)
					writer.WriteStartElement(rdf + ":Description");
				else {
					writer.WriteStartElement(URI(type));
					emittedType = true;
				}
				
				writer.WriteAttributeString(rdf + ":about", resource);
			}
			
			return emittedType;
		}
		
		public override void WriteStatement(string subj, string pred, string obj) {
			if (Open(subj, pred == "http://www.w3.org/1999/02/22-rdf-syntax-ns#type" ? obj : null))
				return;
			writer.WriteStartElement(URI(pred));
			writer.WriteAttributeString("rdf:resource", obj);
			writer.WriteEndElement();
		}
		
		public override void WriteStatementLiteral(string subj, string pred, string literal, string literalType, string literalLanguage) {
			Open(subj, null);
			writer.WriteStartElement(URI(pred));
			writer.WriteString(literal);
			writer.WriteEndElement();
		}
		
		public override string CreateAnonymousNode() {
			return "_:anon" + (anonCounter++);
		}
		
		public override void Dispose() {
			Close();
		}
		
		public override void Close() {
			if (currentSubject != null) {
				writer.WriteEndElement();
				writer.WriteEndElement();
			}
			writer.Close();
		}

		
		private string URI(string uri) {
			if (uri.StartsWith("_:anon")) return uri;
			string ret = autons.Normalize(uri);
			if (ret.StartsWith("<"))
				throw new InvalidOperationException("A namespace prefix must be defined for the URI " + ret + ".");
			return ret;
		}
	}
}
