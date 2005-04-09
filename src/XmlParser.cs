using System;
using System.Collections;
using System.IO;
using System.Xml;

using Drive.Rdf;
using D = Drive.Rdf.RdfXmlParser;
 
using SemWeb;

namespace SemWeb.IO {
	public class RdfXmlParser : RdfParser {
		XmlDocument doc;
		Hashtable resources = new Hashtable();
		Hashtable anons = new Hashtable();
		
		public RdfXmlParser(XmlDocument document) {
			this.doc = document;
		}
		
		public RdfXmlParser(XmlReader document) {
			XmlValidatingReader reader = new XmlValidatingReader(document);
			reader.ValidationType = ValidationType.None;
			doc = new XmlDocument();
			doc.Load(reader);
		}
		
		public RdfXmlParser(TextReader document) : this(new XmlTextReader(document)) {
		}

		public RdfXmlParser(Stream document) : this(new StreamReader(document)) {
		}

		public RdfXmlParser(string file) : this(GetReader(file)) {
		}

		public override void Parse(StatementSinkEx storage) {
			IRdfGraph graph = new D().ParseRdf(doc, BaseUri);
			
			IRdfNTripleCollection triples = graph.GetNTriples();
			for (int i = 0; i < triples.Count; i++) {
				IRdfNTriple tri = triples[i];
				Resource s = Resolve(tri.Subject, storage);
				Resource p = Resolve(tri.Predicate, storage);
				if (!(s is Entity) || !(p is Entity)) continue;
				storage.Add(new Statement((Entity)s, (Entity)p, Resolve(tri.Object, storage), Meta ));
			}
		}
		
		private Resource Resolve(string str, StatementSinkEx storage) {
			if (str.StartsWith("<") && str.EndsWith(">")) {
				string uri = str.Substring(1, str.Length-2);
				Resource ret = (Resource)resources[uri];
				if (ret == null) {
					ret = new Entity(uri);
					resources[uri] = ret;
				}
				return ret;
			}
			if (str.StartsWith("\""))
				return Literal.Parse(str, null);
			
			if (str.StartsWith("_:")) {
				if (anons.ContainsKey(str))
					return (Resource)anons[str];
				Resource r = storage.CreateAnonymousEntity();
				anons[str] = r;
				return r;
			}
			
			throw new ParserException("Invalid resource in graph: " + str);
		}
	}
}

