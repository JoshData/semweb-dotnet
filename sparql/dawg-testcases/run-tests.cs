using System;
using System.IO;

using SemWeb;
using SemWeb.Query;

public class RunTests {
	static int pass = 0, fail = 0, skip = 0;

	public static void Main(string[] args) {
		if (args.Length == 0) {
			Console.Error.WriteLine("Usage: run-tests.exe test-manifest-file.ttl ...");
			return;
		}
		
		foreach (string arg in args) {
			try {
				RunManifestFile(arg);
			} catch (IndexOutOfRangeException) {
				Console.WriteLine("Bad manifest file: " + arg);
			}
		}
			
		Console.WriteLine();
		Console.WriteLine("Tests Passed: " + pass);
		Console.WriteLine("Tests Failed: " + fail);
		Console.WriteLine("Tests Skipped: " + skip);
	}
	
	static void RunManifestFile(string manifestfile) {
		// Load the manifest file
		manifestfile = Path.GetDirectoryName(manifestfile) + "/"; // for good measure
		MemoryStore manifest = new MemoryStore();
		using (RdfReader m = new N3Reader(manifestfile + "/manifest.ttl")) {
			m.BaseUri = manifestfile;
			manifest.Import(m);
		}
		
		// Declare some resources
		
		Entity rdf_first = "http://www.w3.org/1999/02/22-rdf-syntax-ns#first";
		Entity rdf_rest = "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest";
		Entity rdf_nil = "http://www.w3.org/1999/02/22-rdf-syntax-ns#nil";
		
		Entity mf_entries = "http://www.w3.org/2001/sw/DataAccess/tests/test-manifest#entries";
		
		// Get the start of the entries list.
		Entity entries_root = (Entity)manifest.SelectObjects(manifestfile, mf_entries)[0];
		
		// Loop through the tests.
		while (true) {
			Entity test = (Entity)manifest.SelectObjects(entries_root, rdf_first)[0];
			RunTest(test, manifest);
			
			entries_root = (Entity)manifest.SelectObjects(entries_root, rdf_rest)[0];
			if (entries_root == rdf_nil)
				break;
		}
	}

	static void RunTest(Entity test, Store manifest) {
		Entity rdf_type = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
		Entity mf_PositiveSyntaxTest = "http://www.w3.org/2001/sw/DataAccess/tests/test-manifest#PositiveSyntaxTest";
		Entity mf_NegativeSyntaxTest = "http://www.w3.org/2001/sw/DataAccess/tests/test-manifest#NegativeSyntaxTest";
		Entity mf_QueryTest = "http://www.w3.org/2001/sw/DataAccess/tests/test-manifest#QueryEvaluationTest";
		Entity mf_action = "http://www.w3.org/2001/sw/DataAccess/tests/test-manifest#action";
		Entity mf_result = "http://www.w3.org/2001/sw/DataAccess/tests/test-manifest#result";
		Entity qt_data = "http://www.w3.org/2001/sw/DataAccess/tests/test-query#data";
		Entity qt_query = "http://www.w3.org/2001/sw/DataAccess/tests/test-query#query";

		Entity test_type = (Entity)manifest.SelectObjects(test, rdf_type)[0];
		Entity action = (Entity)manifest.SelectObjects(test, mf_action)[0];
		
		if (test_type == mf_PositiveSyntaxTest || test_type == mf_NegativeSyntaxTest) {
			// The action is a query.
			
			// Load the action as a string.
			string q = ReadFile(action.Uri);
				
			// Run the action.
			try {
				SparqlEngine sp = new SparqlEngine(q);
			} catch (SemWeb.Query.QueryFormatException qfe) {
				// On a negative test: Good!
				if (test_type == mf_NegativeSyntaxTest) {
					pass++;
					return;
				}

				Console.WriteLine("Test Failed: " + action);
				Console.WriteLine(qfe.Message);
				Console.WriteLine(q);
				fail++;
				return;
			}
			
			// On a positive test: Good!
			if (test_type == mf_PositiveSyntaxTest) {
				pass++;
				return;
			}

			Console.WriteLine("Test Failed: " + action);
			Console.WriteLine("Query is syntactically incorrect.");
			Console.WriteLine(q);
			fail++;
			
		} else if (test_type == mf_QueryTest) {
		
			Entity data = (Entity)manifest.SelectObjects(action, qt_data)[0];
			Entity query = (Entity)manifest.SelectObjects(action, qt_query)[0];
			Entity result = (Entity)manifest.SelectObjects(test, mf_result)[0];
			
			MemoryStore data_store = new MemoryStore(new N3Reader(data.Uri));
			string q = ReadFile(query.Uri);

			SparqlEngine sp;
			try {
				sp = new SparqlEngine(q);
			} catch (SemWeb.Query.QueryFormatException qfe) {
				Console.WriteLine("Test Failed: " + test + "; Query: " + query);
				Console.WriteLine(qfe.Message);
				Console.WriteLine(q);
				fail++;
				return;
			}
			
			QueryResultBuffer results = new QueryResultBuffer();
			bool results_bool = false;
			try {
				if (sp.Type != SparqlEngine.QueryType.Ask)
					sp.Run(data_store, results);
				else
					results_bool = sp.Ask(data_store);
			} catch (Exception e) {
				Console.WriteLine("Test Failed: " + test + "; Query: " + query);
				Console.WriteLine(q);
				Console.WriteLine(e);
				fail++;
				return;
			}

			if (result.Uri.EndsWith(".ttl")) {
				MemoryStore result_store = new MemoryStore(new N3Reader(result.Uri));
				skip++;
				return;
			} else if (result.Uri.EndsWith(".srx")) {
				skip++;
				return;
			} else if (result.Uri.EndsWith(".rdf")) {
				skip++;
				return;
			} else {
				skip++;
				Console.WriteLine(test + ": Unknown result type " + result.Uri);
			}
			
		} else {
			skip++;
			Console.WriteLine(test + ": Unknown test type " + test_type);
		}
	}

	static string ReadFile(string filename) {
		using (StreamReader file = new StreamReader(filename))
			return file.ReadToEnd();
	}
}
