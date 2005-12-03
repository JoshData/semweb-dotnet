using System;
using System.Collections;
using System.IO;
using System.Web;
using System.Xml;
 
namespace SemWeb.Remote {

	public class SparqlHttpSource : SelectableSource {
	
		string url;
		
		public SparqlHttpSource(string url) {
			this.url = url;
		}
		
		public bool Contains(Statement template) {
			return Select(template, null, true);;
		}
		
		public void Select(StatementSink sink) {
			Select(Statement.All, sink);
		}
		
		public void Select(Statement template, StatementSink sink) {
			Select(template, sink, false);
		}
		
		bool Select(Statement template, StatementSink sink, bool ask) {
			return Select(
				template.Subject == null ? null : new Entity[] { template.Subject },
				template.Predicate == null ? null : new Entity[] { template.Predicate },
				template.Object == null ? null : new Resource[] { template.Object },
				template.Meta == null ? null : new Entity[] { template.Meta },
				sink,
				ask
				);
		}
		
		public void Select(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, StatementSink sink) {
			Select(subjects, predicates, objects, metas, sink, false);
		}
		
		bool Select(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, StatementSink sink, bool ask) {
			// SPARQL doesn't support metas.  Anything but a null or DefaultMeta
			// meta returns no statements immediately.
			if (metas != null && (metas.Length != 1 || metas[0] != Statement.DefaultMeta))
				return false;
		
			string query;
		
			if (subjects != null && subjects.Length == 1
				&& predicates != null && predicates.Length == 1
				&& objects != null && objects.Length == 1) {
				query = "ASK WHERE { " + S(subjects[0]) + " " + S(predicates[0]) + " " + S(objects[0]) + "}";
			} else {
				if (ask)
					query = "ASK";
				else
					query = "SELECT *";
				query += " WHERE { ";
				query += S(subjects, "subject");
				query += " ";
				query += S(predicates, "predicate");
				query += " ";
				query += S(objects, "object");
				query += " }";
			}
			
			XmlDocument result = Load(query);
			
			foreach (XmlElement boolean in result.DocumentElement) {
				if (boolean.Name != "boolean") continue;
				bool ret = boolean.InnerText == "true";
				if (ask)
					return ret;
				else if (ret)
					sink.Add(new Statement(subjects[0], predicates[0], objects[0]));
			}
			
			if (result.DocumentElement.Name != "sparql")
				throw new ApplicationException("Invalid sever response: Not a sparql results document.");
			
			XmlElement bindings = null;
			foreach (XmlElement e in result.DocumentElement)
				if (e.Name == "results")
					bindings = e;
			if (bindings == null)
				throw new ApplicationException("Invalid sever response: No result node.");
			
			Hashtable bnodes = new Hashtable();
			
			foreach (XmlElement binding in bindings) {
				Entity subj = (Entity)GetBinding(binding, "subject", subjects, bnodes);
				Entity pred = (Entity)GetBinding(binding, "predicate", predicates, bnodes);
				Resource obj = GetBinding(binding, "object", objects, bnodes);
				if (!sink.Add(new Statement(subj, pred, obj))) return true;
			}
			
			return true;
		}
		
		string S(Resource[] r, string v) {
			if (r == null) return "?" + v;
			if (r.Length == 1) return S(r[0]);
			throw new NotImplementedException("Multiple values not supported.");
		}
		
		string S(Resource r) {
			if (r is Literal)
				return r.ToString();
			else if (r.Uri != null) {
				if (r.Uri.IndexOf('>') != -1)
					throw new ArgumentException("Invalid URI: " + r.Uri);
				return "<" + r.Uri + ">";
			} else {
				throw new ArgumentException("Blank node in select not supported.");
			}
		}
		
		Resource GetBinding(XmlElement binding, string v, Resource[] values, Hashtable bnodes) {
			if (values != null && values.Length == 1) return values[0];
			
			XmlElement b = (XmlElement)binding.FirstChild;
			while (b != null && b.GetAttribute("name") != v)
				b = (XmlElement)b.NextSibling;
			if (b == null)
				throw new ApplicationException("Invalid sever response: Not all bindings present (" + v + "): " + binding.OuterXml);
			
			b = (XmlElement)b.FirstChild;
			if (b.Name == "uri")
				return new Entity(b.InnerText);
			else if (b.Name == "literal")
				return new Literal(b.InnerText); // datatype/lang
			else if (b.Name == "bnode") {
				string id = b.InnerText;
				if (bnodes.ContainsKey(id)) return (Entity)bnodes[id];
				Entity ret = new Entity(null);
				bnodes[id] = ret;
				return ret;
			}
			throw new ApplicationException("Invalid sever response: " + b.OuterXml);
		}
		
		XmlDocument Load(string query) {
			string qurl = url + "?query=" + System.Web.HttpUtility.UrlEncode(query);
			
			System.Net.WebRequest rq = System.Net.WebRequest.Create(qurl);
			System.Net.WebResponse resp = rq.GetResponse();
			
			string mimetype = resp.ContentType;
			if (mimetype.IndexOf(';') > -1)
				mimetype = mimetype.Substring(0, mimetype.IndexOf(';'));
			
			if (mimetype != "application/sparql-results+xml")
				throw new ApplicationException("The result of the query was not a SPARQL Results document.");

			XmlDocument ret = new XmlDocument();
			ret.Load(new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8));
						
			return ret;
		}
	}
}

