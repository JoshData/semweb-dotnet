using System;
using System.Collections;
using System.IO;
using System.Text;

using SemWeb;
using SemWeb.Stores;

namespace SemWeb.Query {

	public class SparqlXmlQuerySink : QueryResultSink {
		System.Xml.XmlWriter output;
		
		int blankNodeCounter = 0;
		Hashtable blankNodes = new Hashtable();
		
		private static System.Xml.XmlWriter GetWriter(System.IO.TextWriter writer) {
			System.Xml.XmlTextWriter w = new System.Xml.XmlTextWriter(writer);
			w.Formatting = System.Xml.Formatting.Indented;
			return w;
		}
		
		public SparqlXmlQuerySink(TextWriter output)
		 : this(GetWriter(output)) {
		}

		public SparqlXmlQuerySink(System.Xml.XmlWriter output) {
			this.output = output;
		}
		
		public override void Init(VariableBinding[] variables) {
			output.WriteStartElement("sparql");
			output.WriteAttributeString("xmlns", "http://www.w3.org/2001/sw/DataAccess/rf1/result");
			output.WriteStartElement("head");
			foreach (VariableBinding var in variables) {
				if (var.Name == null) continue;
				output.WriteStartElement("variable");
				output.WriteAttributeString("name", var.Name);
				output.WriteEndElement();
			}
			output.WriteEndElement(); // head
			output.WriteStartElement("results");
		}
		
		public override bool Add(VariableBinding[] result) {
			output.WriteStartElement("result");
			foreach (VariableBinding var in result) {
				if (var.Name == null) continue;
				
				output.WriteStartElement(var.Name);
				if (var.Target == null) {
					output.WriteAttributeString("bound", "false");
				} else if (var.Target.Uri != null) {
					output.WriteAttributeString("uri", var.Target.Uri);
				} else if (var.Target is Literal) {
					Literal literal = (Literal)var.Target;
					if (literal.DataType != null)
						output.WriteAttributeString("datatype", literal.DataType);
					if (literal.Language != null)
						output.WriteAttributeString("language", literal.Language);
					output.WriteString(literal.Value);				
				} else {
					string id;
					if (blankNodes.ContainsKey(var.Target))
						id = (string)blankNodes[var.Target];
					else {
						id = "r" + (++blankNodeCounter);
						blankNodes[var.Target] = id;
					}
					output.WriteAttributeString("bnodeid", id);
				}
				
				output.WriteEndElement();
			}
			output.WriteEndElement();
			
			return true;
		}
		
		public override void Finished() {
			output.WriteEndElement(); // results
			output.WriteEndElement(); // sparql
			output.Close();
		}
	}

}	

