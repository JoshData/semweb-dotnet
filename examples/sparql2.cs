using System;

using SemWeb;
using SemWeb.Remote;
using SemWeb.Query;

public class Sparql1 {

	public static void Main() {
		System.Net.ServicePointManager.Expect100Continue = false; // don't send HTTP Expect: headers which confuse some servers
	
		string endpoint = "http://dbpedia.org/sparql";
		
		string query =
"PREFIX owl: <http://www.w3.org/2002/07/owl#>\n" +
"PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>\n" +
"PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>\n" +
"PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>\n" +
"PREFIX foaf: <http://xmlns.com/foaf/0.1/>\n" +
"PREFIX dc: <http://purl.org/dc/elements/1.1/>\n" +
"PREFIX : <http://dbpedia.org/resource/>\n" +
"PREFIX dbpedia2: <http://dbpedia.org/property/>\n" +
"PREFIX dbpedia: <http://dbpedia.org/>\n" +
"PREFIX skos: <http://www.w3.org/2004/02/skos/core#>\n" +
"\n" +
"SELECT ?property ?hasValue ?isValueOf\n" +
"WHERE {\n" +
"  { <http://dbpedia.org/resource/Category:First-person_shooters> ?property ?hasValue }\n" +
"  UNION\n" +
"  { ?isValueOf ?property <http://dbpedia.org/resource/Category:First-person_shooters> }\n" +
"}\n";
      
      
		SparqlHttpSource source = new SparqlHttpSource(endpoint);
		
		Console.WriteLine("RunSparqlQuery(query, Console.Out):");
		source.RunSparqlQuery(query, Console.Out);
		Console.WriteLine();
		
		Console.WriteLine("RunSparqlQuery(query, SparqlXmlQuerySink):");
		source.RunSparqlQuery(query, new SparqlXmlQuerySink(Console.Out));
		Console.WriteLine();
		Console.WriteLine();

	}

}
