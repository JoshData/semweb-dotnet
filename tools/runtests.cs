using System;
using System.Collections;
using System.IO;
using SemWeb;

public class N3Test {

	static string basepath;
	static int total = 0, error = 0, badpass = 0, badfail = 0;
	
	public static void Main(string[] args) {
		if (args.Length != 2) {
			Console.WriteLine("runtests.exe basepath manifestfile");
			return;
		}
		
		basepath = args[0];
		string manifestfile = Path.Combine(basepath, args[1]);
	
		RdfReader reader = new RdfXmlReader(manifestfile);
		if (!manifestfile.EndsWith(".rdf"))
			reader = new N3Reader(manifestfile);
		reader.BaseUri = "http://www.example.org/";
		
		MemoryStore testlist = new MemoryStore(reader);
		
		// RDF/XML TESTS
		
		foreach (Entity test in testlist.GetEntitiesOfType("http://www.w3.org/2000/10/rdf-tests/rdfcore/testSchema#PositiveParserTest"))
			RunTest(testlist, test, true, 0);
		foreach (Entity test in testlist.GetEntitiesOfType("http://www.w3.org/2000/10/rdf-tests/rdfcore/testSchema#NegativeParserTest"))
			RunTest(testlist, test, false, 0);
		
		// N3 TESTS
			
		foreach (Entity test in testlist.GetEntitiesOfType("http://www.w3.org/2004/11/n3test#PositiveParserTest"))
			RunTest(testlist, test, true, 1);
		foreach (Entity test in testlist.GetEntitiesOfType("http://www.w3.org/2004/11/n3test#NegativeParserTest"))
			RunTest(testlist, test, false, 1);
			
		Console.WriteLine("Total Tests:\t{0}", total);
		Console.WriteLine("Total Failures:\t{0} ({1}%)", badfail+badpass, (int)(100*(float)(badpass+badfail)/total));
		Console.WriteLine("Positive Fails:\t{0}", badfail);
		Console.WriteLine("Negative Fails:\t{0}", badpass);
		Console.WriteLine("Test Errors:\t{0}", error);
	}
	
	
	static void RunTest(Store testlist, Entity test, bool shouldSucceed, int mode) {
		string inputpath = null, outputpath = null,
			inputformat = null, outputformat = null,
			inputbase = null, outputbase = null;		
	
		if (mode == 0) {
			inputformat = "xml";
			outputformat = "n3";
		
			string uribase = "http://www.w3.org/2000/10/rdf-tests/rdfcore/";
			Resource input = testlist.SelectObjects(test, "http://www.w3.org/2000/10/rdf-tests/rdfcore/testSchema#inputDocument")[0];
			inputbase = input.Uri;
			inputpath = input.Uri.Substring(uribase.Length);
			
			if (testlist.SelectObjects(test, "http://www.w3.org/2000/10/rdf-tests/rdfcore/testSchema#outputDocument").Length > 0) {
				Resource output = testlist.SelectObjects(test, "http://www.w3.org/2000/10/rdf-tests/rdfcore/testSchema#outputDocument")[0];
				outputpath = output.Uri.Substring(uribase.Length);
				outputbase = output.Uri;
			}
		} else if (mode == 1) {
			inputformat = "n3";
			outputformat = "n3";
			inputbase = "file:/home/syosi/cvs-trunk/WWW/2000/10/swap/test/n3parser.tests";
			
			string uribase = "http://www.example.org/";
			
			if (testlist.SelectObjects(test, "http://www.w3.org/2004/11/n3test#inputDocument").Length == 0) return;
			Resource input = testlist.SelectObjects(test, "http://www.w3.org/2004/11/n3test#inputDocument")[0];
			inputpath = input.Uri.Substring(uribase.Length);
			
			if (testlist.SelectObjects(test, "http://www.w3.org/2004/11/n3test#outputDocument").Length > 0) {
				Resource output = testlist.SelectObjects(test, "http://www.w3.org/2004/11/n3test#outputDocument")[0];
				outputpath = output.Uri.Substring(uribase.Length);
			}
		}
	
		
		string desc = test.ToString();
		try {
			desc += " " + ((Literal)testlist.SelectObjects(test, "http://www.w3.org/2000/10/rdf-tests/rdfcore/testSchema#description")[0]).Value;
		} catch (Exception e) {
		}
		
		try {
			total++;
		
			RdfReader reader = RdfReader.Create(inputformat, Path.Combine(basepath, inputpath));
			reader.BaseUri = inputbase;
			
			MemoryStore inputmodel = new MemoryStore(reader);
			if (reader.Warnings.Count > 0) {
				string warnings = String.Join("; ", (string[])((ArrayList)reader.Warnings).ToArray(typeof(string)));
				throw new ParserException(warnings);
			}
			if (!shouldSucceed) {
				Console.WriteLine(desc + ": Should Not Have Passed **\n");
				badpass++;
				return;
			}
		
			if (shouldSucceed && outputpath != null) {
				RdfReader reader2 = RdfReader.Create(outputformat, Path.Combine(basepath, outputpath));
				reader2.BaseUri = outputbase;
				MemoryStore outputmodel = new MemoryStore(reader2);
				
				CompareModels(inputmodel, outputmodel);
			}
		} catch (System.IO.FileNotFoundException ex) {
			Console.WriteLine(inputpath + " Not Found");
			error++;
		} catch (System.IO.DirectoryNotFoundException ex) {
			Console.WriteLine(inputpath + " Not Found");
			error++;
		} catch (ParserException ex) {
			if (shouldSucceed) {
				Console.WriteLine(desc + ": " + ex.Message + " **");
				Console.WriteLine();
				badfail++;
			}
		}
	}

	static Statement Replace(Statement s, Hashtable map) {
		return new Statement(
			map.ContainsKey(s.Subject) ? (Entity)map[s.Subject] : s.Subject,
			map.ContainsKey(s.Predicate) ? (Entity)map[s.Predicate] : s.Predicate,
			map.ContainsKey(s.Object) ? (Resource)map[s.Object] : s.Object
			);
	}
	
	static ArrayList GetBlankNodes(Store s) {
		ArrayList ret = new ArrayList();
		foreach (Entity e in s.GetEntities())
			if (e.Uri == null) ret.Add(e);
		return ret;
	}
	
	static bool Permute(int[] nodemap, int b) {
		if (nodemap.Length == 0) return false;
		nodemap[0]++;
		for (int i = 0; i < nodemap.Length; i++) {
			if (nodemap[i] != b) return true;
			if (i == nodemap.Length-1) break;
			nodemap[i] = 0;
			nodemap[i+1]++;
		}
		return false;
	}
	
	static void CompareModels(Store a, Store b) {
		string failures = "";
		
		ArrayList abnodes = GetBlankNodes(a);
		ArrayList bbnodes = GetBlankNodes(b);
		
		// If the number of blank nodes differ, of course
		// the stores aren't identical, but for matching
		// purposes we'll add bnodes so they have the
		// same number.
		if (abnodes.Count != bbnodes.Count) failures += "\nInput/output have different number of blank nodes";
		while (abnodes.Count < bbnodes.Count) abnodes.Add(new BNode());
		while (bbnodes.Count < abnodes.Count) bbnodes.Add(new BNode());
		
		// Set up the permutation array.
		int[] nodemap = new int[abnodes.Count];
		
		int mindiffc = int.MaxValue;
		string mindiffs = null;
		
		do {
			// Check that two nodes don't map to the same thing.
			bool dup = false;
			for (int i = 0; i < nodemap.Length; i++) {
				for (int j = 0; j < i; j++) {
					if (nodemap[i] == nodemap[j]) { dup = true; }
				}
			}
			if (dup) continue;
			
			// Create maps
			Hashtable mapab = new Hashtable();
			Hashtable mapba = new Hashtable();
			for (int i = 0; i < nodemap.Length; i++) {
				mapab[abnodes[i]] = bbnodes[nodemap[i]];
				mapba[bbnodes[nodemap[i]]] = abnodes[i];
			}
			
			// Test for differences
			string diff = "";
			int diffct = 0;
			foreach (Statement s in a.Select(Statement.All)) {
				if (!b.Contains(Replace(s, mapab))) {
					diff += "\nInput has: " + s;
					diffct++;
				}
			}
			foreach (Statement s in b.Select(Statement.All)) {
				if (!a.Contains(Replace(s, mapba))) {
					diff += "\nOutput has: " + s;
					diffct++;
				}
			}
			
			if (diffct < mindiffc) {
				mindiffc = diffct;
				mindiffs = diff;
			}
		
		} while (Permute(nodemap, bbnodes.Count));
		
		if (mindiffs != null && mindiffc != 0)
			failures += mindiffs;
			
		if (failures != "")
			throw new ParserException(failures);
	}
}
