using System;
using System.Collections;
using System.IO;

using SemWeb;
using SemWeb.Stores;

namespace SemWeb.Query {

	public class SparqlProtocolServerHandler : System.Web.IHttpHandler {
		public int MaximumLimit = -1;
		public string MimeType = "application/sparql-results+xml";
		
		Hashtable sources = new Hashtable();
	
		bool System.Web.IHttpHandler.IsReusable { get { return true; } }
		
		public virtual void ProcessRequest(System.Web.HttpContext context) {
			try {
				string query = context.Request["query"];
				if (query == null || query.Trim() == "")
					throw new QueryFormatException("No query provided.");
				
				// Buffer the response so that any errors while
				// executing don't get outputted after the response
				// has begun.
				
				MemoryStream buffer = new MemoryStream();

				bool closeAfterQuery;
				string overrideMimeType = null;
				
				SelectableSource source = GetDataSource(out closeAfterQuery);
				try {
					Query sparql = CreateQuery(query);
					TextWriter writer = new StreamWriter(buffer, System.Text.Encoding.UTF8);
					RunQuery(sparql, source, writer);
					writer.Flush();

					if (sparql is SparqlEngine && (((SparqlEngine)sparql).Type == SparqlEngine.QueryType.Construct || ((SparqlEngine)sparql).Type == SparqlEngine.QueryType.Describe))
						overrideMimeType = "text/n3";
				} finally {
					if (closeAfterQuery && source is IDisposable)
						((IDisposable)source).Dispose();
				}
				
				if (overrideMimeType != null)
					context.Response.ContentType = overrideMimeType;
				else if (context.Request["outputMimeType"] == null)
					context.Response.ContentType = MimeType;
				else
					context.Response.ContentType = context.Request["outputMimeType"];

				context.Response.OutputStream.Write(buffer.GetBuffer(), 0, (int)buffer.Length);
				
			} catch (QueryFormatException e) {
				context.Response.ContentType = "text/plain";
				context.Response.StatusCode = 400;
				context.Response.StatusDescription = e.Message;
				context.Response.Write(e.Message);
			} catch (QueryExecutionException e) {
				context.Response.ContentType = "text/plain";
				context.Response.StatusCode = 500;
				context.Response.StatusDescription = e.Message;
				context.Response.Write(e.Message);
			}
		}

		protected virtual SelectableSource GetDataSource(out bool closeAfterQuery) {
			closeAfterQuery = false;
			
			if (System.Web.HttpContext.Current == null)
				throw new InvalidOperationException("This method is not valid outside of an ASP.NET request.");

			string path = System.Web.HttpContext.Current.Request.Path;
			lock (sources) {
				SelectableSource source = (SelectableSource)sources[path];
				if (source != null) return source;

				System.Collections.Specialized.NameValueCollection config = (System.Collections.Specialized.NameValueCollection)System.Configuration.ConfigurationSettings.GetConfig("sparqlSources");
				if (config == null)
					throw new InvalidOperationException("No sparqlSources config section is set up.");

				string spec = config[path];
				if (spec == null)
					throw new InvalidOperationException("No data source is set for the path " + path + ".");
					
				bool reuse = true;
				if (spec.StartsWith("noreuse,")) {
					reuse = false;
					closeAfterQuery = true;
					spec = spec.Substring("noreuse,".Length);
				}

				Store src = Store.Create(spec);
					
				if (reuse)
					sources[path] = src;

				return (SelectableSource)src;
			}
		}
		
		protected virtual Query CreateQuery(string query) {
			Query sparql = new SparqlEngine(query);
			if (MaximumLimit != -1 && (sparql.ReturnLimit == -1 || sparql.ReturnLimit > MaximumLimit)) sparql.ReturnLimit = MaximumLimit;
			return sparql;
		}
		
		protected virtual void RunQuery(Query query, SelectableSource source, TextWriter output) {
			if (System.Web.HttpContext.Current != null
				&& System.Web.HttpContext.Current.Request["outputMimeType"] != null
				&& System.Web.HttpContext.Current.Request["outputMimeType"] == "text/html") {
				query.Run(source, new HTMLQuerySink(output));
				return;
			}

			query.Run(source, output);
		}
		
		private class HTMLQuerySink : QueryResultSink {
			TextWriter output;
			
			public HTMLQuerySink(TextWriter output) { this.output = output; }

			public override void Init(Variable[] variables) {
				output.WriteLine("<table>");
				output.WriteLine("<tr>");
				foreach (Variable var in variables) {
					if (var.LocalName == null) continue;
					output.WriteLine("<th>" + var.LocalName + "</th>");
				}
				output.WriteLine("</tr>");
			}
			
			public override void Finished() {
				output.WriteLine("</table>");
			}
			
			public override bool Add(VariableBindings result) {
				output.WriteLine("<tr>");
				foreach (Variable var in result.Variables) {
					if (var.LocalName == null) continue;
					Resource varTarget = result[var];
					string t = varTarget.ToString();
					if (varTarget is Literal) t = ((Literal)varTarget).Value;
					t = t.Replace("&", "&amp;");
					t = t.Replace("<", "&lt;");
					output.WriteLine("<td>" + t + "</td>");
				}
				output.WriteLine("</tr>");			
				return true;
			}
		}

	}

}
