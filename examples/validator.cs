/* This is what I used to run at rdfabout.com/demo/validator */

using System;
using System.Collections;
using System.IO;
using System.Web;
using SemWeb;

public class Validator {

	public Hashtable Validate() {
		string content = HttpContext.Current.Request.Form["content"];
		string format = HttpContext.Current.Request.Form["format"];
		if (content == null || content.Trim() == "" || format == null) {
			HttpContext.Current.Response.Redirect("index.xpd");
			throw new InvalidOperationException();
		}
			
		StringWriter output = new StringWriter();

		RdfReader reader;
		RdfWriter writer;

		Hashtable response = new Hashtable();

		response["InDocument"] = AddLineNumbers(content);
		
		if (format == "xml") {
			reader = new RdfXmlReader(new StringReader(content));
			writer = new N3Writer(output);
			response["InFormat"] = "RDF/XML";
			response["OutFormat"] = "Notation 3";
		} else if (format == "n3") {
			reader = new N3Reader(new StringReader(content));
			writer = new RdfXmlWriter(output);
			response["OutFormat"] = "RDF/XML";
			response["InFormat"] = "Notation 3";
		} else {
			throw new Exception("Invalid format.");
		}
			
		response["Validation"] = "Syntax validated OK.";
		
		response["OutDocument"] = "";
		response["Triples"] = "";
			
		MemoryStore data = new MemoryStore();
		try {
			data.Import(reader);
		} catch (Exception e) {
			response["Validation"] = "Validation failed: " + e.Message + ".";
			return response;
		} finally {
			if (reader.Warnings.Count > 0) {
				response["Validation"] += "  There were warnings: ";
				foreach (string warning in reader.Warnings)
					response["Validation"] += " " + warning + ".";
			}
		}
		
		writer.Namespaces.AddFrom(reader.Namespaces);
		
		try {
			writer.Write(data);
			writer.Close();
			response["OutDocument"] = output.ToString();
		} catch (Exception e) {
			response["OutDocument"] = e.Message;
		}
		
		StringWriter triplesoutput = new StringWriter();
		using (NTriplesWriter tripleswriter = new NTriplesWriter(triplesoutput)) {
			tripleswriter.Write(data);
		}
		response["Triples"] = triplesoutput.ToString();

		return response;
	}
	
	public static string AddLineNumbers(string str) {
		string[] lines = str.Replace("\r", "").Split('\n', '\r');
		for (int i = 0; i < lines.Length; i++) {
			string num = (i+1).ToString();
			while (num.Length < 5) num += " ";
			lines[i] = num + lines[i];
		}
		return String.Join("\n", lines);
	}

}
