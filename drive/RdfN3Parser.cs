/*****************************************************************************
 * RdfN3Parser.cs
 * 
 * Copyright (c) 2002, Rahul Singh.
 *  
 * This file is part of the Drive RDF Parser.
 * The Drive RDF Parser is free software; you can redistribute it and/or 
 * modify it under the terms of the GNU Lesser General Public License as published 
 * by the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 * 
 * The Drive RDF Parser is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Drive RDF Parser; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 * 
 * Author: 
 * 
 * Rahul Singh
 * kingtiny@DriveRDF.org
 ******************************************************************************/
using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Collections;
using System.Net;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Summary description for RdfN3Parser.
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfN3Parser : RdfParser, IRdfN3Parser
	{
		private Regex tripleRegex;
		private Regex uriRegex;
		private Regex blankUriRegex;
		private Regex objLiteralRegex;
		private Regex commentRegex;

		/// <summary>
		/// A collection of N-Triples generated as a result of the last parse  operation
		/// </summary>
		private RdfNTripleCollection _nTriples;

		/// <summary>
		/// Gets a collection of N-Triples generated as a result of the last parse  operation
		/// </summary>
		public IRdfNTripleCollection NTriples
		{
			get
			{
				return _nTriples;
			}
		}

		/// <summary>
		/// Initialzes a new instance of an RDF N-Triple Parser
		/// </summary>
		public RdfN3Parser()
		{
			_nTriples = null;
			_rdfGraph = null;
			_warnings = new ArrayList();
			_errors = new ArrayList();
			StopOnErrors = false;
			StopOnWarnings = false;

			tripleRegex = new Regex(@"^(\S+)\s+(\S+)\s+(\S.*\S[^\.])\.?\s*$", RegexOptions.Compiled);
			uriRegex = new Regex(@"^<(\s*)(.*)(\s*)>",RegexOptions.IgnoreCase | RegexOptions.Singleline| RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);
			blankUriRegex = new Regex(@"^(\s*)_\:(.*)",RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);
			//matches at 1, 3 and 5
			objLiteralRegex = new Regex(@""" \s* (.*)\s* "" \s*(@ \s*(\S+))?\s* (\^\^ \s*<\s* (.*)\s*>)?",RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);
			commentRegex = new Regex(@"^#", RegexOptions.Compiled); 
		}

		/// <summary>
		/// Parses the RDF at the given uri
		/// </summary>
		/// <param name="uri">The source uri for the data to parse</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Uri uri)
		{
			return ParseRdf(uri, null);
		}

		/// <summary>
		/// Parses the RDF at the given uri into an existing graph
		/// </summary>
		/// <param name="uri">The source uri for the data to parse</param>
		/// M<param name="graph">Th graph to use as the destination of the data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Uri uri, IRdfGraph graph)
		{
			WebRequest wReq = WebRequest.Create(uri);
			WebResponse wRes = wReq.GetResponse();
			return ParseRdf(wRes.GetResponseStream(),graph);
		}

		/// <summary>
		/// Parses the RDF at the given uri
		/// </summary>
		/// <param name="uri">The source uri for the data to parse</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(string uri)
		{
			return ParseRdf(uri, null);
		}

		/// <summary>
		/// Parses the RDF at the given uri into an existing graph
		/// </summary>
		/// <param name="uri">The source uri for the data to parse</param>
		/// <param name="graph">Th graph to use as the destination of the data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(string uri, IRdfGraph graph)
		{
			Uri srcUri = null;
			try
			{
				srcUri = new Uri(uri);
			}
			catch(UriFormatException)
			{
				srcUri = new Uri(Path.GetFullPath(uri));
			}
			return ParseRdf(srcUri, graph);
		}


		/// <summary>
		/// Parses the RDF from the given stream
		/// </summary>
		/// <param name="inStream">The source stream for the data to parse</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Stream inStream)
		{
			return ParseRdf(inStream, null);
		}
		
		/// <summary>
		/// Parses the RDF from the given stream into an existing graph
		/// </summary>
		/// <param name="inStream">The source stream for the data to parse</param>
		/// <param name="graph">Th graph to use as the destination of the data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Stream inStream, IRdfGraph graph)
		{
			if(graph != null)
				_rdfGraph = graph;
			else //Create the RDFGraph
				_rdfGraph = new RdfGraph();
			//Create the new NTriple collection
			_nTriples = new RdfNTripleCollection();
			StreamReader sReader = new StreamReader(inStream);
			string data = null;
			while(sReader.Peek() > -1)
			{
				data = sReader.ReadLine();
				ParseData(data);
			}
			return _rdfGraph;
		}

		private string[] ParseTriple (string tripleLine) 
		{
			Match match = null;
			if(commentRegex.IsMatch(tripleLine))
				return null;
			if(tripleLine.Trim().Length == 0)
				return null;
			match = tripleRegex.Match(tripleLine);
			if (match.Success) 
			{
				string subjectString = match.Groups[1].Value;
				string predicateString = match.Groups[2].Value;
				string objectString = match.Groups[3].Value;
				return new string[3] {subjectString, predicateString, objectString};
			} 
			OnError("Invalid Triple: "+tripleLine);
			return null;
		}
		
		private void ParseData(string data)
		{
			string[] triple = ParseTriple(data);
			string subject = null;
			string predicate = null;
			string obj = null;
			if(triple == null)
				return;
			
			try
			{
				subject = triple[0];
				predicate = triple[1];
				obj = triple[2];
			}
			catch(ArgumentOutOfRangeException)
			{
				OnError("Malformed Triple "+data);
				return;
			}

			RdfNTriple nTriple = new RdfNTriple(subject, predicate, obj);
			NTriples.Add(nTriple);
			
			string subjectUri = GetUri(subject);
			string predicateUri = GetUri(predicate);
			string objUri = GetUri(obj);
			string literalVal = null;
			string langID = null;
			string datatypeUri = null;
			if(objUri == null)
				SplitLiteral(obj, ref literalVal, ref langID, ref datatypeUri);

			if(subjectUri == null)
			{
				OnError("Invalid Subject: "+subject);
				return;
			}
			if(predicateUri == null)
			{
				OnError("Invalid predicate: "+predicate);
				return;
			}

			if((objUri == null) && (literalVal == null))
			{
				OnError("Invalid object: "+obj);
				return;
			}

			if(!IsValidUri(subjectUri))
			{
				OnError("Invalid pubject Uri: "+subjectUri);
				return;
			}

			if(!IsValidUri(predicateUri))
			{
				OnError("Invalid predicate Uri: "+subjectUri);
				return;
			}

			if((objUri != null) && (!IsValidUri(objUri)))
			{
				OnError("Invalid object Uri: "+objUri);
				return;
			}

			if((objUri == null) && (datatypeUri != null) && (!IsValidUri(datatypeUri)))
			{
				OnError("Invalid datatype Uri: "+objUri);
				return;
			}

			RdfNode parentNode = AddNodeToGraph(subjectUri);
			RdfEdge childEdge = new RdfEdge(predicateUri);
			parentNode.AttachChildEdge(childEdge);
			
			RdfNode childNode;
			if(objUri!= null)
				childNode = AddNodeToGraph(objUri);
			else
				childNode = AddLiteralToGraph(literalVal, langID, datatypeUri);
			childEdge.AttachChildNode(childNode);

			//add the edge to the edge to the graph
			AddEdgeToGraph(childEdge);
		}

		private string GetUri(string str)
		{
			Match match = null;
			string uriStr = null;
			//is str a blank node Uri
			match = blankUriRegex.Match(str);
			if(match.Success)
			{
				uriStr = "blankID:"+match.Groups[2];
				return uriStr;
			}
			
			match = uriRegex.Match(str);
			if(match.Success)
			{
				uriStr = match.Groups[2].Value;
				return uriStr;
			}
			return null;
		}

		private void SplitLiteral(string str, ref string literalVal, ref string langID, ref string datatypeUri)
		{
			Match match = null;
			literalVal = null;
			langID = null;
			datatypeUri = null;
			//first check if its a literal with a langID and a datatype
			match = objLiteralRegex.Match(str);
			if(match.Success)
			{
				literalVal = match.Groups[1].Value;
				literalVal = literalVal.Replace("\\n","\x0d\x0a");
				literalVal = literalVal.Replace("\\\"","\"");
				langID = match.Groups[3].Value;
				datatypeUri = match.Groups[5].Value;
				if((langID != null) && (langID.Length == 0))
					langID = null;
				if((datatypeUri != null) && (datatypeUri.Length == 0))
					datatypeUri = null;

			}
		}
	}
}
