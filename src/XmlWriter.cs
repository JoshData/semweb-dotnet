using System;
using System.Collections;
using System.IO;
using System.Text;
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
		Hashtable nameAlloc = new Hashtable();
		
		static Entity rdftype = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
		
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
			
			string rdfprefix = ns.GetPrefix(NS.RDF);
			if (rdfprefix == null) {
				if (ns.GetNamespace("rdf") == null) {
					rdfprefix = "rdf";
					ns.AddNamespace(NS.RDF, "rdf");
				}
			}
			
			XmlElement root = doc.CreateElement(rdfprefix + ":RDF", NS.RDF);
			foreach (string prefix in ns.GetPrefixes())
				root.SetAttribute("xmlns:" + prefix, ns.GetNamespace(prefix));
			doc.AppendChild(root);
		}
		
		public override NamespaceManager Namespaces { get { return ns; } }
		
		char[] normalizechars = { '#', '/' };
		
		private void Normalize(string uri, out string prefix, out string localname) {
			if (uri == "")
				throw new InvalidOperationException("The empty URI cannot be used as an element node.");
				
			if (BaseUri == null && uri.StartsWith("#")) {
				// This isn't quite right, but it prevents dieing
				// for something not uncommon in N3.  The hash
				// gets lost.
				prefix = "";
				localname = uri.Substring(1);
				return;
			}
		
			if (ns.Normalize(uri, out prefix, out localname))
				return;
				
			// No namespace prefix was registered, so come up with something.
			
			int last = uri.LastIndexOfAny(normalizechars);
			if (last <= 0)
				throw new InvalidOperationException("No namespace was registered and no prefix could be automatically generated for <" + uri + ">");
				
			int prev = uri.LastIndexOfAny(normalizechars, last-1);
			if (prev <= 0)
				throw new InvalidOperationException("No namespace was registered and no prefix could be automatically generated for <" + uri + ">");
			
			string n = uri.Substring(0, last+1);
			localname = uri.Substring(last+1);
			
			// TODO: Make sure the local name (here and anywhere in this
			// class) is a valid XML name.
			
			if (Namespaces.GetPrefix(n) != null) {
				prefix = Namespaces.GetPrefix(n);
				return;
			}
			
			prefix = uri.Substring(prev+1, last-prev-1);
			
			// Remove all non-xmlable (letter) characters.
			StringBuilder newprefix = new StringBuilder();
			foreach (char c in prefix)
				if (char.IsLetter(c))
					newprefix.Append(c);
			prefix = newprefix.ToString();
			
			if (prefix.Length == 0) {
				// There were no letters in the prefix!
				prefix = "ns";
			}
			
			if (Namespaces.GetNamespace(prefix) == null) {
				doc.DocumentElement.SetAttribute("xmlns:" + prefix, n);
				Namespaces.AddNamespace(n, prefix);
				return;
			}
			
			int ctr = 1;
			while (true) {
				if (Namespaces.GetNamespace(prefix + ctr) == null) {
					prefix += ctr;
					doc.DocumentElement.SetAttribute("xmlns:" + prefix, n);
					Namespaces.AddNamespace(n, prefix);
					return;
				}
				ctr++;
			}
		}
		
		private void SetAttribute(XmlElement element, string nsuri, string prefix, string localname, string val) {
			XmlAttribute attr = doc.CreateAttribute(prefix, localname, nsuri);
			attr.InnerXml = val;
			element.SetAttributeNode(attr);
		}
		
		private XmlElement GetNode(Entity entity, string type, XmlElement context) {
			string uri = entity.Uri;
		
			if (nodeMap.ContainsKey(entity)) {
				XmlElement ret = (XmlElement)nodeMap[entity];
				if (type == null) return ret;
				
				// Check if we have to add new type information to the existing node.
				if (ret.NamespaceURI + ret.LocalName == NS.RDF + "Description") {
					// Replace the untyped node with a typed node, copying in
					// all of the children of the old node.
					string prefix, localname;
					Normalize(type, out prefix, out localname);
					XmlElement newnode = doc.CreateElement(prefix + ":" + localname, ns.GetNamespace(prefix));
					
					foreach (XmlNode childnode in ret) {
						newnode.AppendChild(childnode.Clone());
					}
					
					ret.ParentNode.ReplaceChild(newnode, ret);
					nodeMap[entity] = newnode;
					return newnode;
				} else {
					// The node is already typed, so just add a type predicate.
					XmlElement prednode = CreatePredicate(ret, NS.RDF + "type");
					SetAttribute(prednode, NS.RDF, ns.GetPrefix(NS.RDF), "resource", type);
					return ret;
				}
			}
			
			Start();			
			
			XmlElement node;
			if (type == null) {
				node = doc.CreateElement(ns.GetPrefix(NS.RDF) + ":Description", NS.RDF);
			} else {
				string prefix, localname;
				Normalize(type, out prefix, out localname);
				node = doc.CreateElement(prefix + ":" + localname, ns.GetNamespace(prefix));
			}
			
			if (uri != null) {
				SetAttribute(node, NS.RDF, ns.GetPrefix(NS.RDF), "about", uri);
			} else {
				SetAttribute(node, NS.RDF, ns.GetPrefix(NS.RDF), "nodeID", GetBNodeRef((BNode)entity));
			}
			
			if (context == null)
				doc.DocumentElement.AppendChild(node);
			else
				context.AppendChild(node);
			
			nodeMap[entity] = node;
			return node;
		}
		
		private XmlElement CreatePredicate(XmlElement subject, Entity predicate) {
			if (predicate.Uri == null)
				throw new InvalidOperationException("Predicates cannot be blank nodes.");
			
			string prefix, localname;
			Normalize(predicate.Uri, out prefix, out localname);
			XmlElement pred = doc.CreateElement(prefix + ":" + localname, ns.GetNamespace(prefix));
			subject.AppendChild(pred);
			return pred;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
		
			XmlElement subjnode;
			
			bool hastype = statement.Predicate == rdftype && statement.Object.Uri != null;
			subjnode = GetNode(statement.Subject, hastype ? statement.Object.Uri : null, null);
			if (hastype) return;

			XmlElement prednode = CreatePredicate(subjnode, statement.Predicate);
			
			if (!(statement.Object is Literal)) {
				if (nodeMap.ContainsKey(statement.Object)) {
					if (statement.Object.Uri != null) {
						SetAttribute(prednode, NS.RDF, ns.GetPrefix(NS.RDF), "resource", statement.Object.Uri);
					} else {
						SetAttribute(prednode, NS.RDF, ns.GetPrefix(NS.RDF), "nodeID", GetBNodeRef((BNode)statement.Object));
					}
				} else {
					GetNode((Entity)statement.Object, null, prednode);
				}
			} else {
				Literal literal = (Literal)statement.Object;
				prednode.InnerText = literal.Value;
				if (literal.Language != null)
					prednode.SetAttribute("xml:lang", literal.Language);
				if (literal.DataType != null)
					SetAttribute(prednode, NS.RDF, ns.GetPrefix(NS.RDF), "datatype", literal.DataType);
			}
		}
		
		public string GetBNodeRef(BNode node) {
			if (node.LocalName != null &&
				(nameAlloc[node.LocalName] == null || (BNode)nameAlloc[node.LocalName] == node)
				&& !node.LocalName.StartsWith("bnode")) {
				nameAlloc[node.LocalName] = node; // ensure two different nodes with the same local name don't clash
				return node.LocalName;
			} else if (anonAlloc[node] != null) {
				return (string)anonAlloc[node];
			} else {
				string id = "bnode" + (anonCounter++);
				anonAlloc[node] = id;
				return id;
			}
		}
		
		public override void Close() {
			base.Close();
			if (doc != null)
				doc.WriteTo(writer);
			writer.Close();
		}
	}

}
