using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml;

using SemWeb.Util;

namespace SemWeb {
	public class RdfXmlReader : RdfReader {
		XmlReader xml;
		
		Hashtable blankNodes = new Hashtable();
		UriMap namedNodes = new UriMap();
		
		StatementSink storage;
		
		static readonly Entity
			rdfType = NS.RDF + "type",
			rdfFirst = "http://www.w3.org/1999/02/22-rdf-syntax-ns#first",
			rdfRest = "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest",
			rdfNil = "http://www.w3.org/1999/02/22-rdf-syntax-ns#nil";
		
		public RdfXmlReader(XmlDocument document) {
			xml = new XmlNodeReader(document);
		}
		
		public RdfXmlReader(XmlReader document) {
			XmlValidatingReader reader = new XmlValidatingReader(document);
			reader.ValidationType = ValidationType.None;
			xml = reader;
		}
		
		public RdfXmlReader(TextReader document) : this(new XmlTextReader(document)) {
		}

		public RdfXmlReader(Stream document) : this(new StreamReader(document)) {
		}

		public RdfXmlReader(string file) : this(GetReader(file)) {
		}
		
		public override void Parse(StatementSink storage) {
			// Read past the processing instructions to
			// the document element.  If it is rdf:RDF,
			// then process the description nodes within it.
			// Otherwise, the document element is itself a
			// description.
			
			this.storage = storage;
									
			while (xml.Read()) {
				if (xml.NodeType != XmlNodeType.Element) continue;
				
				if (xml.NamespaceURI == NS.RDF && xml.LocalName == "RDF" ) {
					while (xml.Read()) {
						if (xml.NodeType == XmlNodeType.Element)
							ParseDescription();
					}
					
				} else {
					ParseDescription();
				
				}
				break;
			}

			xml.Close();
		}
		
		private string CurNode() {
			return xml.NamespaceURI + xml.LocalName;
		}
		
		private int isset(string attribute) {
			return attribute != null ? 1 : 0;
		}
		
		private string Unrelativize(string uri) {
			if (!uri.StartsWith("#") && uri != "") return uri;
			if (xml.BaseURI == "" && BaseUri == null)
				OnError("A relative URI was used in the document but the document has no base URI");
			if (xml.BaseURI != "")
				return xml.BaseURI + uri;
			else
				return BaseUri + uri;
		}
		
		private Entity GetBlankNode(string nodeID) {
			if (blankNodes.ContainsKey(nodeID))
				return (Entity)blankNodes[nodeID];
			
			Entity entity = new Entity(null);
			blankNodes[nodeID] = entity;

			return entity;
		}
		
		private Entity GetNamedNode(string uri) {
			if (!ReuseEntities)
				return new Entity(uri);
		
			Entity ret = (Entity)namedNodes[uri];
			if (ret != null) return ret;
			ret = new Entity(uri);
			namedNodes[uri] = ret;
			return ret;
		}
		
		private Entity ParseDescription() {
			// The XmlReader is positioned on an element node
			// that is a description of an entity.
			// On returning, the reader is positioned after the
			// end element of the description node.
			
			string nodeID = xml.GetAttribute("nodeID", NS.RDF);
			string about = xml.GetAttribute("about", NS.RDF);
			string ID = xml.GetAttribute("ID", NS.RDF);
			if (isset(nodeID) + isset(about) + isset(ID) > 1)
				OnError("An entity description cannot specify more than one of rdf:nodeID, rdf:about, and rdf:ID");
				
			Entity entity;
			
			if (about != null)
				entity = GetNamedNode(Unrelativize(about));
			else if (ID != null)
				entity = GetNamedNode(Unrelativize("#" + ID));
			else if (nodeID != null)
				entity = GetBlankNode(nodeID);
			else
				entity = new Entity(null);
			
			// If the name of the element is not rdf:Description,
			// then the name gives its type.
			if (CurNode() != NS.RDF + "Description") {
				storage.Add(new Statement(entity, rdfType, (Entity)CurNode()));
			}
			
			ParsePropertyAttributes(entity);
			ParsePropertyNodes(entity);
			
			return entity;
		}
		
		private void ParsePropertyAttributes(Entity entity) {
			if (!xml.MoveToFirstAttribute()) return;
			do {
				// rdf:type is interpreted with an entity object,
				// not a literal object.
				if (CurNode() == NS.RDF + "type") {
					storage.Add(new Statement(entity, rdfType, (Entity)xml.Value));
					continue;
				}
				
				// Properties which are not recognized as property
				// attributes.
				if (CurNode() == NS.RDF + "RDF") continue;
				if (CurNode() == NS.RDF + "Description") continue;
				if (CurNode() == NS.RDF + "ID") continue;
				if (CurNode() == NS.RDF + "about") continue;
				if (CurNode() == NS.RDF + "parseType") continue;
				if (CurNode() == NS.RDF + "resource") continue;
				if (CurNode() == NS.RDF + "li") continue;
				if (CurNode() == NS.RDF + "nodeID") continue;
				if (CurNode() == NS.RDF + "datatype") continue;
				
				// This is a literal property attribute.
				storage.Add(new Statement(entity, CurNode(),
					new Literal(xml.Value, xml.XmlLang, null)));
					
			} while (xml.MoveToNextAttribute());
			
			xml.MoveToElement();
		}
		
		private void ParsePropertyNodes(Entity subject) {
			// The reader is positioned within a description node.
			// On returning, the reader is positioned after the
			// end element of the description node.
			
			if (xml.IsEmptyElement) return;
			
			int liIndex = 0;
			
			while (xml.Read()) {
				if (xml.NodeType == XmlNodeType.EndElement)
					break;
				if (xml.NodeType != XmlNodeType.Element)
					continue;
				
				ParseProperty(subject, ref liIndex);
			}
		}
		
		private void ParseProperty(Entity subject, ref int liIndex) {
			// The reader is positioned on a propert node,
			// and on returning the reader is positioned past
			// that node.
			
			string nodeID = xml.GetAttribute("nodeID", NS.RDF);
			string resource = xml.GetAttribute("resource", NS.RDF);
			
			string parseType = xml.GetAttribute("parseType", NS.RDF);
			string datatype = xml.GetAttribute("datatype", NS.RDF);
			
			string lang = xml.XmlLang != "" ? xml.XmlLang : null;
			
			Resource objct = null;
			if (nodeID != null || resource != null) {
				if (isset(nodeID) + isset(resource) > 1)
					OnError("A predicate node cannot specify more than one of rdf:nodeID and rdf:resource");
					
				if (parseType != null || datatype != null)
					OnError("The attributes rdf:parseType and rdf:datatype are not valid on a predicate with a rdf:nodeID or rdf:resource attribute");
					
				// Object is an entity given by nodeID or resource.
				// The 
				if (nodeID != null)
					objct = GetBlankNode(nodeID);
				else if (resource != null)
					objct = GetNamedNode(Unrelativize(resource));
				
				// No children are allowed in this element.
				if (!xml.IsEmptyElement)
				while (xml.Read()) {
					if (xml.NodeType == XmlNodeType.EndElement) break;
					if (xml.NodeType == XmlNodeType.Whitespace) continue;
					OnError("Content is not allowed within a property with a rdf:nodeID or rdf:resource attribute");
				}
			
			} else if (parseType != null && parseType == "Literal") {
				objct = new Literal(xml.ReadInnerXml(), lang, datatype);
				
			} else if (parseType != null && parseType == "Resource") {
				if (xml.IsEmptyElement)
					OnError("Expecting a resource description");
				
				objct = new Entity(null);
				ParsePropertyAttributes((Entity)objct);
				ParsePropertyNodes((Entity)objct);
				
			} else if (parseType != null && parseType == "Collection") {
				Entity collection = new Entity(null);
				Entity lastnode = collection;
				bool empty = true;
				
				if (!xml.IsEmptyElement)
				while (xml.Read()) {
					if (xml.NodeType == XmlNodeType.EndElement) break;
					if (xml.NodeType != XmlNodeType.Element) continue;
					Entity item = ParseDescription();
					
					storage.Add(new Statement(lastnode, rdfFirst, item));
					
					Entity next = new Entity(null);
					storage.Add(new Statement(lastnode, rdfRest, next));
					lastnode = next;
					
					empty = false;
				}
								
				storage.Add(new Statement(lastnode, rdfRest, rdfNil));
				
				if (empty)
					objct = rdfNil;
				else
					objct = collection;
				
			} else if (datatype != null) {
				// Forces even xml content to be read as in parseType=Literal?
				objct = new Literal(xml.ReadInnerXml(), lang, datatype);
			
			} else {
				// We don't know whether the contents of this element
				// refer to a literal or an entity.  If an element is
				// a child of this node, then it must be an entity.
				// Otherwise the text content is the literal value.
				
				StringBuilder textcontent = new StringBuilder();
				bool hadText = false;
				bool hadElement = false;
				
				if (!xml.IsEmptyElement)
				while (xml.Read()) {
					if (xml.NodeType == XmlNodeType.EndElement) break;
					if (xml.NodeType == XmlNodeType.Element) {
						if (hadText)
							OnError("Both text and elements are present as a property value");
						hadElement = true;
						
						objct = ParseDescription();
					} else if (xml.NodeType == XmlNodeType.Text || xml.NodeType == XmlNodeType.SignificantWhitespace) {
						if (hadElement)
							OnError("Both text and elements are present as a property value");
						textcontent.Append(xml.Value);
						hadText = true;
					} else {
						textcontent.Append(xml.Value);
					}
				}
				
				if (!hadElement)
					objct = new Literal(textcontent.ToString(), lang, null);
				
			}
				
			string predicate = CurNode();
			if (predicate == NS.RDF + "li")
				predicate = NS.RDF + "_" + (liIndex++);
				
			string ID = xml.GetAttribute("ID", NS.RDF);
			if (ID == null) {
				// Not reified
				storage.Add(new Statement(subject, predicate, objct));
			} else {
				// Reified
				Entity statement = GetBlankNode(ID);
				storage.Add(new Statement(statement, NS.RDF + "subject", subject));
				storage.Add(new Statement(statement, NS.RDF + "predicate", (Entity)predicate));
				storage.Add(new Statement(statement, NS.RDF + "object", objct));
				storage.Add(new Statement(statement, NS.RDF + "type", (Entity)(NS.RDF + "Statement")));
			}
		}
		
		private void OnError(string message) {
			if (xml is IXmlLineInfo && ((IXmlLineInfo)xml).HasLineInfo()) {
				IXmlLineInfo line = (IXmlLineInfo)xml;
				message += ", line " + line.LineNumber + " col " + line.LinePosition;
			}
			throw new ParserException(message);
		}
	}
}

