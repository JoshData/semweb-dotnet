using System;
using System.Collections;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;

using SemWeb;
using SemWeb.Util;

[assembly: AssemblyTitle("RDFXSLTProc - XSLT Engine over RDF Data")]
[assembly: AssemblyCopyright("Copyright (c) 2005 Joshua Tauberer <tauberer@for.net")]
[assembly: AssemblyDescription("A tool for running XSLT stylesheets over an RDF data model.")]

[assembly: Mono.UsageComplement("datasource [rootentity] stylesheet")]

public class SWXSLTProc {
	class Opts : Mono.GetOptions.Options {
		[Mono.GetOptions.Option("A {name=value} parameter to pass to the stylesheet.  value is a string, no quotes required.")]
		public string[] param;
	}

	public static void Main(string[] args) {
		Opts opts = new Opts();
		opts.ProcessArgs(args);
		if (opts.RemainingArguments.Length != 2 && opts.RemainingArguments.Length != 3) {
			opts.DoHelp();
			return;
		}

		string datasource, rootentity, stylesheetfile;
		if (opts.RemainingArguments.Length == 2) {
			datasource = opts.RemainingArguments[0];
			rootentity = null;
			stylesheetfile = opts.RemainingArguments[1];
		} else {
			datasource = opts.RemainingArguments[0];
			rootentity = opts.RemainingArguments[1];
			stylesheetfile = opts.RemainingArguments[2];
		}
		
		XsltArgumentList xsltargs = new XsltArgumentList();
		
		if (opts.param != null)
		foreach (string p in opts.param) {
			int eq = p.IndexOf('=');
			if (eq == -1) {
				Console.Error.WriteLine("Param arguments must be name=value.");
				return;
			}
			string n = p.Substring(0, eq);
			string v = p.Substring(eq+1);
			xsltargs.AddParam(n, "", v);
		}

		XmlDocument sty = new XmlDocument();
		sty.PreserveWhitespace = true;
		sty.Load(stylesheetfile);

		XslTransform t = new XslTransform();
		t.Load(sty, null, null);

		NamespaceManager nsmgr = new NamespaceManager();

		// Scan xmlsn: attributes in the stylesheet node.
		// For all namespaces of the form assembly://assemblyname/TypeName, load in
		// the methods of that type as extension functions in that namespace.
		// And add xmlns declarations to nsmgr.
		foreach (XmlAttribute a in sty.DocumentElement.Attributes) {
			if (!a.Name.StartsWith("xmlns:")) continue;
			
			nsmgr.AddNamespace(a.Value, a.Name.Substring(6));
			
			System.Uri uri = new System.Uri(a.Value);
			if (uri.Scheme != "assembly") continue;
			System.Reflection.Assembly assembly = System.Reflection.Assembly.Load(uri.Host);
			System.Type ty = assembly.GetType(uri.AbsolutePath.Substring(1));
			if (ty == null) {
				Console.Error.WriteLine("Type not found: " + uri);
				return;
			}
			object obj = ty.GetConstructor(new Type[0]).Invoke(new Type[0]); 
			xsltargs.AddExtensionObject(a.Value, obj);
		}
		
		StatementSource source = Store.CreateForInput(datasource);
		Store model;
		if (source is Store) model = (Store)source;
		else model = new MemoryStore(source);
		
		XPathNavigator nav;
		if (rootentity != null)
			nav = new XPathSemWebNavigator(rootentity, model, nsmgr);
		else
			nav = new XPathSemWebNavigator(model, nsmgr);

		t.Transform(nav, xsltargs, Console.Out, null);
	}
}

