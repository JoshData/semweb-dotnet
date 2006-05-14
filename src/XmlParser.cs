using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml;

using SemWeb.Util;

namespace SemWeb {
	public class RdfXmlReader : RdfReader {
		// TODO: Make some of the errors warnings.
	
		XmlReader xml;
		
		Hashtable blankNodes = new Hashtable();
		UriMap namedNodes = new UriMap();
		Hashtable seenIDs = new Hashtable();
		
		StatementSink storage;
		
		static readonly Entity
			rdfType = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type",
			rdfFirst = "http://www.w3.org/1999/02/22-rdf-syntax-ns#first",
			rdfRest = "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest",
			rdfNil = "http://www.w3.org/1999/02/22-rdf-syntax-ns#nil",
			rdfSubject = "http://www.w3.org/1999/02/22-rdf-syntax-ns#subject",
			rdfPredicate = "http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate",
			rdfObject = "http://www.w3.org/1999/02/22-rdf-syntax-ns#object",
			rdfStatement = "http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement";
		
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
		
		public override void Select(StatementSink storage) {
			// Read past the processing instructions to
			// the document element.  If it is rdf:RDF,
			// then process the description nodes within it.
			// Otherwise, the document element is itself a
			// description.
			
			this.storage = storage;
									
			while (xml.Read()) {
				if (xml.NodeType != XmlNodeType.Element) continue;
				
				if (xml.NamespaceURI == NS.RDF && xml.LocalName == "RDF" ) {
					// If there is an xml:base here, set BaseUri so
					// the application can recover it.  It doesn't
					// affect parsing since the xml:base attribute
					// will override BaseUri.
					string xmlbase = xml.GetAttribute("xml:base");
					if (xmlbase != null) BaseUri = xmlbase;
					
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
			if (xml.NamespaceURI == "")
				OnWarning("Element node must be qualified (" + xml.Name + ")");
			if (xml.Prefix != "")
				Namespaces.AddNamespace(xml.NamespaceURI, xml.Prefix);

			// This probably isn't quite right, but compensates
			// for a corresponding hash issue in XmlWriter.
			if (xml.NamespaceURI == "" && BaseUri == null)
				return "#" + xml.LocalName;

			return xml.NamespaceURI + xml.LocalName;
		}
		
		private int isset(string attribute) {
			return attribute != null ? 1 : 0;
		}
		
		private string Unrelativize(string uri) {
			return GetAbsoluteUri(xml.BaseURI != "" ? xml.BaseURI : BaseUri, uri);
		}
		
		private Entity GetBlankNode(string nodeID) {
			if (blankNodes.ContainsKey(nodeID))
				return (Entity)blankNodes[nodeID];
			
			Entity entity = new BNode(nodeID);
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

			if (nodeID != null && !IsValidXmlName(nodeID))
				OnWarning("'" + nodeID + "' is not a valid XML Name");
			if (ID != null && !IsValidXmlName(ID))
				OnWarning("'" + ID + "' is not a valid XML Name");
				
			Entity entity;
			
			if (about != null)
				entity = GetNamedNode(Unrelativize(about));
			else if (ID != null) {
				entity = GetNamedNode(Unrelativize("#" + ID));
				
				if (seenIDs.ContainsKey(entity.Uri))
					OnWarning("Two descriptions cannot use the same rdf:ID: <" + entity.Uri + ">");
				seenIDs[entity.Uri] = seenIDs;
			} else if (nodeID != null)
				entity = GetBlankNode(nodeID);
			else
				entity = new BNode();
			
			// If the name of the element is not rdf:Description,
			// then the name gives its type.
			if (CurNode() != NS.RDF + "Description") {
				if (IsRestrictedName(CurNode()) || IsDeprecatedName(CurNode()))
					OnError(xml.Name + " cannot be the type of a resource.");
				if (CurNode() == NS.RDF + "li") OnError("rdf:li cannot be the type of a resource");
				storage.Add(new Statement(entity, rdfType, (Entity)CurNode(), Meta));
			}
			
			ParsePropertyAttributes(entity);
			ParsePropertyNodes(entity);
			
			return entity;
		}
		
		private bool ParsePropertyAttributes(Entity entity) {
			bool foundAttrs = false;
			
			if (!xml.MoveToFirstAttribute()) return false;
			do {
				// Propery attributes in the default namespace
				// should be ignored.
				if (xml.NamespaceURI == "")
					continue;
			
				string curnode = CurNode();
				
				// rdf:type is interpreted with an entity object,
				// not a literal object.
				if (curnode == NS.RDF + "type") {
					storage.Add(new Statement(entity, rdfType, (Entity)xml.Value, Meta));
					foundAttrs = true;
					continue;
				}
				
				// Properties which are not recognized as property
				// attributes and should be ignored.
				if (IsRestrictedName(curnode))
					continue;
				if (IsDeprecatedName(curnode))
					OnError(xml.Name + " is deprecated.");
				
				// Properties which are invalid as attributes.
				if (curnode == NS.RDF + "li")
					OnError("rdf:li is not a valid attribute");
				if (curnode == NS.RDF + "aboutEach" || curnode == NS.RDF + "aboutEachPrefix")
					OnError("rdf:aboutEach has been removed from the RDF spec");
				
				// Unrecognized attributes in the xml namespace should be ignored.
				if (xml.Prefix == "xml") continue;
				if (xml.Prefix == "xmlns") continue;
				if (curnode == "http://www.w3.org/2000/xmlns/xmlns") continue;
				
				// This is a literal property attribute.
				string lang = xml.XmlLang != "" ? xml.XmlLang : null;
				storage.Add(new Statement(entity, curnode,
					new Literal(xml.Value, lang, null), Meta));
				Namespaces.AddNamespace(xml.NamespaceURI, xml.Prefix);
				foundAttrs = true;
					
			} while (xml.MoveToNextAttribute());
			
			xml.MoveToElement();
			
			return foundAttrs;
		}
		
		private void ParsePropertyNodes(Entity subject) {
			// The reader is positioned within a description node.
			// On returning, the reader is positioned after the
			// end element of the description node.
			
			if (xml.IsEmptyElement) return;
			
			int liIndex = 1;
			
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
			
			// Get all of the attributes before we move the reader forward.
			
			string nodeID = xml.GetAttribute("nodeID", NS.RDF);
			string resource = xml.GetAttribute("resource", NS.RDF);
			
			string parseType = xml.GetAttribute("parseType", NS.RDF);
			string datatype = xml.GetAttribute("datatype", NS.RDF);
			
			string lang = xml.XmlLang != "" ? xml.XmlLang : null;

			string predicate = CurNode();
			if (predicate == NS.RDF + "li")
				predicate = NS.RDF + "_" + (liIndex++);

			if (IsRestrictedName(predicate))
				OnError(xml.Name + " cannot be used as a property name.");
			if (IsDeprecatedName(predicate))
				OnError(xml.Name + " has been deprecated and cannot be used as a property name.");

			string ID = xml.GetAttribute("ID", NS.RDF);

			if (nodeID != null && !IsValidXmlName(nodeID))
				OnWarning("'" + nodeID + "' is not a valid XML Name");
			if (ID != null && !IsValidXmlName(ID))
				OnWarning("'" + ID + "' is not a valid XML Name");
				
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
					
				ParsePropertyAttributes((Entity)objct);
				
				// No children are allowed in this element.
				if (!xml.IsEmptyElement)
				while (xml.Read()) {
					if (xml.NodeType == XmlNodeType.EndElement) break;
					if (xml.NodeType == XmlNodeType.Whitespace) continue;
					if (xml.NodeType == XmlNodeType.Comment) continue;
					if (xml.NodeType == XmlNodeType.ProcessingInstruction) continue;
					OnError("Content is not allowed within a property with a rdf:nodeID or rdf:resource attribute");
				}
			
			} else if (parseType != null && parseType == "Literal") {
				if (datatype == null)
					datatype = "http://www.w3.org/1999/02/22-rdf-syntax-ns#XMLLiteral";
				
				if (ParsePropertyAttributes(new BNode()))
					OnError("Property attributes are not valid when parseType is Literal");

				// TODO: Do we canonicalize according to:
				// http://www.w3.org/TR/2002/REC-xml-exc-c14n-20020718/ ?
				
				objct = new Literal(xml.ReadInnerXml(), null, datatype);

			} else if (parseType != null && parseType == "Resource") {
				objct = new BNode();
				
				ParsePropertyAttributes((Entity)objct);
				if (!xml.IsEmptyElement)
					ParsePropertyNodes((Entity)objct);
				
			} else if (parseType != null && parseType == "Collection") {
				Entity collection = new BNode();
				Entity lastnode = collection;
				bool empty = true;
				
				ParsePropertyAttributes(collection);
				
				if (!xml.IsEmptyElement)
				while (xml.Read()) {
					if (xml.NodeType == XmlNodeType.EndElement) break;
					if (xml.NodeType != XmlNodeType.Element) continue;
					
					if (!empty) {
						Entity next = new BNode();
						storage.Add(new Statement(lastnode, rdfRest, next, Meta));
						lastnode = next;
					}
					
					Entity item = ParseDescription();
					storage.Add(new Statement(lastnode, rdfFirst, item, Meta));
					
					empty = false;
				}

				storage.Add(new Statement(lastnode, rdfRest, rdfNil, Meta));
				
				if (empty)
					objct = rdfNil;
				else
					objct = collection;
					
			} else if (parseType != null) {
				OnError("Invalid value for parseType: '" + parseType + "'");
				
			} else if (datatype != null) {
				// Note that any xml:lang is discarded.
				
				if (ParsePropertyAttributes(new BNode()))
					OnError("Property attributes are not valid when a datatype is given");
					
				if (xml.IsEmptyElement) {
					objct = new Literal("", null, datatype);
				} else {
					objct = new Literal(xml.ReadString(), null, datatype);
					if (xml.NodeType != XmlNodeType.EndElement)
						OnError("XML markup may not appear in a datatyped literal property.");
				}
			
			} else {
				// We don't know whether the contents of this element
				// refer to a literal or an entity.  If an element is
				// a child of this node, then it must be an entity.
				// If the property has predicate attributes, then it
				// is an anonymous entity.  Otherwise the text content
				// is the literal value.
				
				objct = new BNode();
				if (ParsePropertyAttributes((Entity)objct)) {
					// Found property attributes.  There should be no other internal content?
					
					if (!xml.IsEmptyElement)
					while (xml.Read()) {
						if (xml.NodeType == XmlNodeType.EndElement) break;
						if (xml.NodeType == XmlNodeType.Whitespace) continue;
						if (xml.NodeType == XmlNodeType.Comment) continue;
						if (xml.NodeType == XmlNodeType.ProcessingInstruction) continue;
						OnError(xml.NodeType + " is not allowed within a property with property attributes");
					}
					
				} else {
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
			}
				
			storage.Add(new Statement(subject, predicate, objct, Meta));
			
			if (ID != null) {
				// In addition to adding the statement as normal, also
				// add a reified statement.
				Entity statement = GetNamedNode(Unrelativize("#" + ID));;
				storage.Add(new Statement(statement, rdfType, rdfStatement, Meta));
				storage.Add(new Statement(statement, rdfSubject, subject, Meta));
				storage.Add(new Statement(statement, rdfPredicate, (Entity)predicate, Meta));
				storage.Add(new Statement(statement, rdfObject, objct, Meta));
			}
		}
		
		private bool IsRestrictedName(string name) {
			if (name == NS.RDF + "RDF") return true;
			if (name == NS.RDF + "Description") return true;
			if (name == NS.RDF + "ID") return true;
			if (name == NS.RDF + "about") return true;
			if (name == NS.RDF + "parseType") return true;
			if (name == NS.RDF + "resource") return true;
			if (name == NS.RDF + "nodeID") return true;
			if (name == NS.RDF + "datatype") return true;
			return false;
		}
		
		private bool IsDeprecatedName(string name) {
			if (name == NS.RDF + "bagID") return true;
			if (name == NS.RDF + "aboutEach") return true;
			if (name == NS.RDF + "aboutEachPrefix") return true;
			return false;
		}

		private bool IsValidXmlName(string name) {
			// I'm not sure what's supposed to be valid, but this is just for warnings anyway.
			// CombiningChar and Extender characters are omitted.
			if (name.Length == 0) return false;
			char f = name[0];
			if (!char.IsLetter(f) && f != '_') return false;
			for (int i = 1; i < name.Length; i++) {
				f = name[i];
				if (!char.IsLetter(f) && !char.IsDigit(f) && f != '.' && f != '-' && f != '_')
					return false;
			}
			return true;
		}
		
		private void OnError(string message) {
			if (xml is IXmlLineInfo && ((IXmlLineInfo)xml).HasLineInfo()) {
				IXmlLineInfo line = (IXmlLineInfo)xml;
				message += ", line " + line.LineNumber + " col " + line.LinePosition;
			}
			throw new ParserException(message);
		}

		private new void OnWarning(string message) {
			if (xml is IXmlLineInfo && ((IXmlLineInfo)xml).HasLineInfo()) {
				IXmlLineInfo line = (IXmlLineInfo)xml;
				message += ", line " + line.LineNumber + " col " + line.LinePosition;
			}
			base.OnWarning(message);
		}
	}
}

