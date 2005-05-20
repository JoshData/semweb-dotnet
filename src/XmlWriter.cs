using System;
using System.Collections;
using System.IO;
using System.Xml;

using SemWeb;

namespace SemWeb {
	public class RdfXmlWriter : RdfWriter {
		XmlWriter writer;
		NamespaceManager ns;
		
		XmlDocument doc;
		
		Hashtable nodeMap = new Hashtable();
		
		long anonCounter = 0;
		Hashtable anonAlloc = new Hashtable();
		
		public RdfXmlWriter(string file) : this(file, null) { }
		
		public RdfXmlWriter(string file, NamespaceManager ns) : this(GetWriter(file), ns) { }

		public RdfXmlWriter(TextWriter writer) : this(writer, null) { }
		
		public RdfXmlWriter(TextWriter writer, NamespaceManager ns) : this(NewWriter(writer), ns) { }
		
		private static XmlWriter NewWriter(TextWriter writer) {
			XmlTextWriter ret = new XmlTextWriter(writer);
			ret.Formatting = Formatting.Indented;
			ret.Indentation = 1;
			ret.IndentChar = '\t';
			ret.Namespaces = true;
			return ret;
		}
		
		public RdfXmlWriter(XmlWriter writer) : this(writer, null) { }
		
		public RdfXmlWriter(XmlWriter writer, NamespaceManager ns) {
			if (ns == null)
				ns = new NamespaceManager();
			this.writer = writer;
			this.ns = ns;
		}
		
		private void Start() {
			if (doc != null) return;
			doc = new XmlDocument();
			XmlElement root = doc.CreateElement(ns.GetPrefix(NS.RDF) + ":RDF", NS.RDF);
			foreach (string prefix in ns.GetPrefixes())
				root.SetAttribute("xmlns:" + prefix, ns.GetNamespace(prefix));
			doc.AppendChild(root);
		}
		
		public override NamespaceManager Namespaces { get { return ns; } }
		
		private XmlElement GetNode(string uri, string type, XmlElement context) {
			if (nodeMap.ContainsKey(uri))
				return (XmlElement)nodeMap[uri];
			
			Start();			
			
			XmlElement node;
			if (type == null) {
				node = doc.CreateElement(ns.GetPrefix(NS.RDF) + ":Description", NS.RDF);
			} else {
				string prefix, localname;
				if (!ns.Normalize(type, out prefix, out localname))
					throw new InvalidOperationException("No prefix is registered for the namespace of " + uri);
				node = doc.CreateElement(prefix + ":" + localname, ns.GetNamespace(prefix));
			}
			
			if (!anonAlloc.ContainsKey(uri)) {
				node.SetAttribute(ns.GetPrefix(NS.RDF) + ":about", NS.RDF, uri);
				anonAlloc.Remove(uri);
			}
			
			if (context == null)
				doc.DocumentElement.AppendChild(node);
			else
				context.AppendChild(node);
			
			nodeMap[uri] = node;
			return node;
		}
		
		private XmlElement CreatePredicate(XmlElement subject, string predicate) {
			string prefix, localname;
			if (!ns.Normalize(predicate, out prefix, out localname))
				throw new InvalidOperationException("No prefix is registered for the namespace of " + predicate);
			XmlElement pred = doc.CreateElement(prefix + ":" + localname, ns.GetNamespace(prefix));
			subject.AppendChild(pred);
			return pred;
		}
		
		public override void WriteStatement(string subj, string pred, string obj) {
			XmlElement subjnode = GetNode(subj, pred == "http://www.w3.org/1999/02/22-rdf-syntax-ns#type" ? obj : null, null);
			if (pred == "http://www.w3.org/1999/02/22-rdf-syntax-ns#type") return;
			
			XmlElement prednode = CreatePredicate(subjnode, pred);
			if (nodeMap.ContainsKey(obj)) {
				prednode.SetAttribute(ns.GetPrefix(NS.RDF) + ":resource", NS.RDF, obj);
			} else {
				GetNode(obj, null, prednode);
			}
		}
		
		public override void WriteStatement(string subj, string pred, Literal literal) {
			XmlElement subjnode = GetNode(subj, null, null);
			XmlElement prednode = CreatePredicate(subjnode, pred);
			prednode.InnerText = literal.Value;
		}
		
		public override string CreateAnonymousEntity() {
			string id = "_:anon" + (anonCounter++);
			anonAlloc[id] = anonAlloc;
			return id;
		}
		
		public override void Close() {
			base.Close();
			if (doc != null)
				doc.WriteTo(writer);
			writer.Close();
		}
	}

	internal class RdfXmlWriter2 : RdfWriter {
		XmlWriter writer;
		NamespaceManager ns;
		NamespaceManager autons;
		
		long anonCounter = 0;
		string currentSubject = null;
		
		string rdf;
		
		public RdfXmlWriter2(string file) : this(file, null) { }
		
		public RdfXmlWriter2(string file, NamespaceManager ns) : this(GetWriter(file), ns) { }

		public RdfXmlWriter2(TextWriter writer) : this(writer, null) { }
		
		public RdfXmlWriter2(TextWriter writer, NamespaceManager ns) : this(NewWriter(writer), ns) { }
		
		private static XmlWriter NewWriter(TextWriter writer) {
			XmlTextWriter ret = new XmlTextWriter(writer);
			ret.Formatting = Formatting.Indented;
			ret.Indentation = 1;
			ret.IndentChar = '\t';
			return ret;
		}
		
		public RdfXmlWriter2(XmlWriter writer) : this(writer, null) { }
		
		public RdfXmlWriter2(XmlWriter writer, NamespaceManager ns) {
			if (ns == null)
				ns = new NamespaceManager();
			this.writer = writer;
			this.ns = ns;
			//autons = new AutoPrefixNamespaceManager(this.ns);
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
		
		public override void WriteStatement(string subj, string pred, Literal literal) {
			Open(subj, null);
			writer.WriteStartElement(URI(pred));
			// Should write language and datatype
			writer.WriteString(literal.Value);
			writer.WriteEndElement();
		}
		
		public override string CreateAnonymousEntity() {
			return "_:anon" + (anonCounter++);
		}
		
		public override void Close() {
			base.Close();
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
