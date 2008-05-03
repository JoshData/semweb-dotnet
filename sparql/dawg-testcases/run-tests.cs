using System;
using System.Collections;
using System.IO;
using System.Text;

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
				new SparqlEngine(q);
			} catch (SemWeb.Query.QueryFormatException qfe) {
				// On a negative test: Good!
				if (test_type == mf_NegativeSyntaxTest) {
					pass++;
					return;
				}

				Console.WriteLine("Test Failed: " + action);
				Console.WriteLine(qfe.Message);
				Console.WriteLine(q);
				Console.WriteLine();
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
			Console.WriteLine();
			fail++;
			
		} else if (test_type == mf_QueryTest) {
		
			Entity data = (Entity)manifest.SelectObjects(action, qt_data)[0];
			Entity query = (Entity)manifest.SelectObjects(action, qt_query)[0];
			Entity result = (Entity)manifest.SelectObjects(test, mf_result)[0];
			
			MemoryStore data_store = new MemoryStore(new N3Reader(data.Uri));
			string q = ReadFile(query.Uri);
			
			if (q.IndexOf("ASK") >= 0) {
				Console.WriteLine("ASK Test Skipped: " + test);
				skip++;
				return;
			}
			
			string run_individual_test = "mono ../../bin/rdfquery.exe -type sparql n3:" + data.Uri + " < " + query.Uri;

			SparqlEngine sp;
			try {
				sp = new SparqlEngine(q);
			} catch (SemWeb.Query.QueryFormatException qfe) {
				Console.WriteLine("Test Failed: " + test);
				Console.WriteLine(run_individual_test);
				Console.WriteLine(q);
				Console.WriteLine(qfe.Message);
				Console.WriteLine();
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
				Console.WriteLine("Test Failed: " + test);
				Console.WriteLine(run_individual_test);
				Console.WriteLine(q);
				Console.WriteLine(e);
				Console.WriteLine();
				fail++;
				return;
			}
			
			bool failed = false;
			StringBuilder info = new StringBuilder();

			if (result.Uri.EndsWith(".ttl") || result.Uri.EndsWith(".srx") || result.Uri.EndsWith(".rdf")) {
			
				bool sorted = false;
				QueryResultBuffer results2 = new QueryResultBuffer();
				
				if (result.Uri.EndsWith(".srx")) {
						using (FileStream fs = new FileStream(result.Uri, FileMode.Open))
							SemWeb.Remote.SparqlHttpSource.ParseSparqlResponse(fs, results2);
							
				} else if (result.Uri.EndsWith(".rdf") || result.Uri.EndsWith(".ttl")) {
					RdfReader reader = null;
					if (result.Uri.EndsWith(".rdf"))
						reader = new RdfXmlReader(result.Uri);
					else if (result.Uri.EndsWith(".ttl"))
						reader = new N3Reader(result.Uri);
					MemoryStore result_store = new MemoryStore(reader);
					
					string rs = "http://www.w3.org/2001/sw/DataAccess/tests/result-set#";
					Entity rsResultSet = rs + "ResultSet";
					Entity rsresultVariable = rs + "resultVariable";
					Entity rssolution = rs + "solution";
					Entity rsindex = rs + "index";
					Entity rsbinding = rs + "binding";
					Entity rsvariable = rs + "variable";
					Entity rsvalue = rs + "value";
				
					// get a list of variables in the query output
					Entity resultset = result_store.GetEntitiesOfType(rsResultSet)[0];
					ArrayList vars = new ArrayList();
					foreach (Literal var in result_store.SelectObjects(resultset, rsresultVariable))
						vars.Add(new Variable(var.Value));
					Variable[] varsarray = (Variable[])vars.ToArray(typeof(Variable));
					
					// try to order as best we can to our own output, so we sort the results the same way
					for (int i = 0; i < results.Variables.Length; i++) {
						if (i >= varsarray.Length) break;
						for (int j = i; j < varsarray.Length; j++) {
							if (varsarray[j].LocalName == results.Variables[i].LocalName) {
								Variable temp = varsarray[i];
								varsarray[i] = varsarray[j];
								varsarray[j] = temp;
								break;
							}
						}
					}
					
					Hashtable varmap = new Hashtable();
					foreach (Variable v in varsarray)
							varmap[v.LocalName] = varmap.Count;
					
					results2.Init(varsarray);
					
					Resource[] resultbindings = result_store.SelectObjects(resultset, rssolution);
					
					// Try sorting by index
					int[] indexes = new int[resultbindings.Length];
					for (int i = 0; i < resultbindings.Length; i++) {
						Entity binding = (Entity)resultbindings[i];
						Literal index = (Literal)result_store.SelectObjects(binding, rsindex)[0];
						indexes[i] = (int)(Decimal)index.ParseValue();
						sorted = true;
					}
					Array.Sort(indexes, resultbindings);
					
					// Add bindings into results2.
					for (int i = 0; i < resultbindings.Length; i++) {
						Resource[] row = new Resource[vars.Count];
						Entity binding = (Entity)resultbindings[i];
						foreach (Entity var in result_store.SelectObjects(binding, rsbinding)) {
							string name = ((Literal)result_store.SelectObjects(var, rsvariable)[0]).Value;
							Resource val = result_store.SelectObjects(var, rsvalue)[0];
							row[(int)varmap[name]] = val;
						}
						results2.Add(new VariableBindings(varsarray, row));
					}
				}
				
				// Check variable list
				ArrayList vars1 = new ArrayList();
				foreach (Variable v in results.Variables)
					vars1.Add(v.LocalName);				
				ArrayList vars2 = new ArrayList();
				foreach (Variable v in results2.Variables)
					vars2.Add(v.LocalName);				
				failed |= !SetsSame(vars1, vars2, "Result Set Variables", info);

				// Checking bindings
				if (results.Bindings.Count != results2.Bindings.Count) {
					info.Append("Solutions have different number of bindings.\n");
					failed = true;
				} else {
					// Now actually run comparison.
					
					if (!sorted) {
						((ArrayList)results.Bindings).Sort();
						((ArrayList)results2.Bindings).Sort();
					}
					
					for (int i = 0; i < results.Bindings.Count; i++) {
							VariableBindings b1 = (VariableBindings)results.Bindings[i];
							VariableBindings b2 = (VariableBindings)results2.Bindings[i];
							foreach (Variable var in results.Variables) {
								Resource val1 = b1[var.LocalName];
								Resource val2 = b2[var.LocalName];
								if (val1 != val2 && !(val1 is BNode) && !(val2 is BNode)) { // TODO: Test bnodes are returned correctly
									info.Append("Binding row " + i + " differ in value of " + var.LocalName + " variable: " + val2 + ", should be: " + val1 + "\n");
									failed = true;
								}
							}
					}
				}
				
			} else {
				skip++;
				Console.WriteLine(test + ": Unknown result type " + result.Uri);
			}
			
			if (failed) {
				Console.WriteLine("Test Failed: " + test);
				Console.WriteLine(run_individual_test);
				Console.WriteLine(q);
				Console.WriteLine(info.ToString());
				Console.WriteLine();
				fail++;
			} else {
				pass++;
			}
			
		} else {
			skip++;
			Console.WriteLine(test + ": Unknown test type " + test_type);
			Console.WriteLine();
		}
	}

	static string ReadFile(string filename) {
		using (StreamReader file = new StreamReader(filename))
			return file.ReadToEnd();
	}
	
	static bool SetsSame(ICollection a, ICollection b, string s, StringBuilder sb) {
		return SetsSame2(a, b, s + ": Element in computed set not in gold set: ", sb) && SetsSame2(b, a, s + ": Element in gold set not in computed set: ", sb);
	}
	static bool SetsSame2(ICollection a, ICollection b, string s, StringBuilder sb) {
		bool ok = true;
		Hashtable x = new Hashtable();
		foreach (object o in a)
			x[o] = o;
		foreach (object o in b) {
			if (x[o] == null) {
				sb.Append(s + ": " + o + "\n");
				ok = false;
			}
		}
		return ok;
	}
}
