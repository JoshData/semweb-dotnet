// This file has been modified from the Drive RDF/XML parser to fit
// into the structure of this assembly.

/*****************************************************************************
 * RdfXmlParser.cs
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
using System.Xml;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// The primary RDF Parser.
	/// </summary>
	public class RdfXmlParser : RdfParser, IRdfXmlParser
	{
		/// <summary>
		/// A Collection of non syntactic elements in the RDF syntax
		/// </summary>
		/// <remarks>The members of this collection are rdf:Alt rdf:Seq rdf:Bag rdf:rest rdf:first rdf:predicate rdf:object rdf:List rdf:subject rdf:Statement rdf:Property rdf:datatype rdf:value</remarks>
		private Hashtable _nonSyntacticRdfElements;

		/// <summary>
		/// A Collection of syntactic elements in the RDF syntax
		/// </summary>
		/// <remarks>The members of this collection are rdf:RDF, rdf:ID, rdf:about, rdf:resource, rdf:parseType, rdf:li, rdf:Description, rdf:nodeID</remarks>
		private Hashtable _syntacticRdfElements;

		/// <summary>
		/// A Collection of all RDF and XML properties
		/// </summary>
		/// <remarks>The members of this collection are rdf:about, rdf:resource, rdf:parseType, rdf:ID, rdf:nodeID, rdf:datatype, rdf:value, xml:lang, xml:base</remarks>
		private Hashtable _rdfXmlProperties;

		/// <summary>
		/// A table that that maps RDF Nodes to the hignest ordinal used for rdf:li properties
		/// </summary>
		private Hashtable _rdfLiTable;

		/// <summary>
		/// A Hashtable that stores declared rdf:ID values for quick lookup of duplicates
		/// </summary>
		private Hashtable _declID;

		/// <summary>
		/// Variable used to generate new IDs for RDF Nodes
		/// </summary>
		private long _newID;
		
		/// <summary>
		/// Initializes an instance of the RdfParser class
		/// </summary>
		public RdfXmlParser()
		{
			_rdfGraph = null;
			_warnings = new ArrayList();
			_errors = new ArrayList();
			_newID = 10000;
			string[] rdfXmlAttrs = {"rdf:about", "rdf:resource", "rdf:parseType", "rdf:ID", "rdf:nodeID", "rdf:type", "rdf:datatype", "rdf:value", "xml:lang", "xml:base"};
			_rdfXmlProperties = new Hashtable();
			int len = rdfXmlAttrs.Length;
			for(int i=0;i<len;i++)
				_rdfXmlProperties.Add(rdfXmlAttrs[i],rdfXmlAttrs[i]);

			string[] nonSyntacticRdfElements = {"rdf:Alt", "rdf:Seq", "rdf:Bag", "rdf:rest", "rdf:first", "rdf:predicate", "rdf:object", "rdf:List", "rdf:subject", "rdf:Statement", "rdf:Property", "rdf:datatype", "rdf:value"};
			_nonSyntacticRdfElements = new Hashtable();
			len = nonSyntacticRdfElements.Length;
			for(int i=0;i<len;i++)
				_nonSyntacticRdfElements.Add(nonSyntacticRdfElements[i], nonSyntacticRdfElements[i]);

			string[] syntacticRdfElements = {"rdf:RDF", "rdf:ID", "rdf:about", "rdf:resource", "rdf:parseType", "rdf:li", "rdf:Description", "rdf:nodeID"};
			_syntacticRdfElements = new Hashtable();
			len = syntacticRdfElements.Length;
			for(int i=0;i<len;i++)
				_syntacticRdfElements.Add(syntacticRdfElements[i], syntacticRdfElements[i]);

			_declID = new Hashtable();
			_rdfLiTable = new Hashtable();

			StopOnErrors = false;
			StopOnWarnings = false;
		}

		/// <summary>
		/// Parses the RDF from the given TextReader, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(TextReader txtReader, IRdfGraph graph, string xmlbaseUri)
		{
			//parses the graph from the TextReader
			//looks for xml:base from the xml document
			//if it is not found, then uses the xml:base specified by xmlbaseUri
			//if xmlbaseUri is null or not a valid Uri it defaults to http://unknown.org/
			if(txtReader == null)
				throw(new ArgumentNullException("The specified Text Reader is a null reference"));

			XmlDocument doc = new XmlDocument();
			try
			{
				doc.Load(txtReader);
			}
			catch(XmlException xe)
			{
				OnError(xe);
				return null;
			}
			catch(Exception e)
			{
				OnError(e);
				return null;
			}
			return ParseRdf(doc, graph, xmlbaseUri);
		}

		/// <summary>
		/// Parses the RDF from the given TextReader, using the given xml:base uri
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(TextReader txtReader, string xmlbaseUri)
		{
			return ParseRdf(txtReader,null,xmlbaseUri);
		}

		/// <summary>
		/// Parses the RDF from the given TextReader, into an existing graph
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(TextReader txtReader, IRdfGraph graph)
		{
			return ParseRdf(txtReader, graph, null);
		}
		
		/// <summary>
		/// When implemented by a class, parses the RDF from the given TextReader
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(TextReader txtReader)
		{
			//parses the graph from the TextReader
			//looks for xml:base from the xml document
			//if it is not found, defaults to http://unknown.org/
			return ParseRdf(txtReader, null, null);
		}


		/// <summary>
		/// Parses the RDF from the given XmlReader, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlReader reader, IRdfGraph graph, string xmlbaseUri)
		{
			if(reader == null)
				throw(new ArgumentNullException("The specified Xml Reader is a null reference"));

			XmlDocument doc = new XmlDocument();
			try
			{
				doc.Load(reader);
			}
			catch(XmlException xe)
			{
				OnError(xe);
				return null;
			}
			catch(Exception e)
			{
				OnError(e);
				return null;
			}
			return ParseRdf(doc,graph, xmlbaseUri);
		}

		/// <summary>
		/// Parses the RDF from the given XmlReader, using the given xml:base uri
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlReader reader, string xmlbaseUri)
		{
			//parses the graph from the XmlReader
			//looks for xml:base from the xml document
			//if it is not found, then uses the xml:base specified by xmlbaseUri
			//if xmlbaseUri is null or not a valid Uri it defaults to http://unknown.org/
			return ParseRdf(reader, null, xmlbaseUri);
		}

		/// <summary>
		/// Parses the RDF from the given XmlReader, into an existing graph
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlReader reader, IRdfGraph graph)
		{
			//parses the graph from the XmlReader
			//looks for xml:base from the xml document
			//if it is not found, then uses the xml:base specified by xmlbaseUri
			//if xmlbaseUri is null or not a valid Uri it defaults to http://unknown.org/
			return ParseRdf(reader, graph, null);
		}

		/// <summary>
		/// Parses the RDF from the given XmlReader
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlReader reader)
		{
			//parses the graph from the XmlReader
			//looks for xml:base from the xml document
			//if it is not found, defaults to http://unknown.org/
			return ParseRdf(reader, null, null);
		}


		/// <summary>
		/// Parses the RDF from the given XmlDocument, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlDocument doc, IRdfGraph graph, string xmlbaseUri)
		{
			//parses from the xml document
			//if doc is null throws an ArgumentNullException
			//looks for xml:base in doc
			//if xml:base is not found in doc then uses the xmlbaseUri 
			//if xmlbaseUri is not a valid Uri it defaults to http://unknown.org/
			if(doc == null)
				throw(new ArgumentNullException("The specified XmlDocument object is a null reference"));

			Warnings.Clear();
			Errors.Clear();
			//Start with the root
			XmlElement root = doc.DocumentElement;
			
			if(root.Name != "rdf:RDF")
			{
				if(root.Name.ToLower() == "rdf")
					OnWarning("Unqualified use of rdf as the root element name.");
				else
					OnWarning("Root element of an RDF document must be rdf:RDF");
			}
			string oldXmlbaseUri = null;
			if(graph == null)
			{
				//Now create the RDFGraph
				_rdfGraph = new RdfGraph();
			}
			else
			{
				oldXmlbaseUri = graph.NameSpaces["xml:base"];
				graph.NameSpaces.Remove("xml:base");
				_rdfGraph = graph;
			}

			//its an RDF Document so now get the namespace info
			int count = root.Attributes.Count;
			for(int i=0;i<count;i++)
			{
				try
				{
					string nsName = root.Attributes[i].Name;
					if(_rdfGraph.NameSpaces[nsName] != null)
						OnWarning("Redefinition of namespace "+nsName);
					Debug.Assert(nsName != null);
					Debug.Assert(root.Attributes[i].Value != null);
					_rdfGraph.NameSpaces[nsName] = root.Attributes[i].Value;
				}
				catch(ArgumentException ine)
				{
					OnWarning(ine.Message);
				}
			}
			
			string xbUri = _rdfGraph.NameSpaces["xml:base"];
			if(xbUri == null)
			{
				xbUri = doc.BaseURI;
				if(!IsValidUri(xbUri))
				{
					xbUri = xmlbaseUri;
					if(!IsValidUri(xbUri))
					{
						if(oldXmlbaseUri != null)
							xbUri = oldXmlbaseUri;
						else
						{
							OnWarning("Valid xml:base URI not found. Using http://unknown.org/");
							xbUri = "http://unknown.org/";
						}
					}
				}
			}
			Debug.Assert(xbUri != null);
			
			//ignore and discard everything after the first # character
			int pos = xbUri.IndexOf('#');
			if(pos != -1)
			{
				xbUri = xbUri.Remove(pos,xbUri.Length-pos);
			}
			//Now finally set the value of the xml:base Uri
			_rdfGraph.NameSpaces["xml:base"] = xbUri;

			if(root.HasChildNodes)
			{
				count = root.ChildNodes.Count;
				for(int i=0;i<count;i++)
					ProcessNode(root.ChildNodes[i], true,null);
			}
		
			return _rdfGraph;
		}
	
		/// <summary>
		/// Parses the RDF from the given XmlDocument, using the given xml:base uri
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlDocument doc, string xmlbaseUri)
		{
			return ParseRdf(doc,null,xmlbaseUri);
		}

		/// <summary>
		/// Parses the RDF from the given XmlDocument, into an existing graph
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlDocument doc, IRdfGraph graph)
		{
			return ParseRdf(doc,graph,null);
		}

		/// <summary>
		/// Parses the RDF from the given XmlDocument
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(XmlDocument doc)
		{
			//parses from the xml document
			//if doc is null throws an ArgumentNullException
			//looks for xml:base in doc
			//if xml:base is not found then defaults to http://unknown.org/
			if(doc == null)
				throw(new ArgumentNullException("The specified XmlDocument is a null reference"));
			return ParseRdf(doc,null, null);
		}

		
		/// <summary>
		/// Parses the RDF at the given URI, into an existing graph
		/// </summary>
		/// <param name="uri">The Uri of the document to parse</param>
		/// <param name="graph">An object that implements the IRdfGraph interface that will be used as the destination graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Uri uri, IRdfGraph graph)
		{
			if(uri == null)
				throw(new ArgumentNullException("The specified URI is a null reference"));
			//parses from a Uri.
			XmlDocument doc = new XmlDocument();
			try
			{
				doc.Load(uri.ToString());
			}
			catch(XmlException xe)
			{
				OnError(xe);
				return null;
			}
			catch(Exception e)
			{
				OnError(e);
				return null;
			}
			return ParseRdf(doc, graph);
		}

		/// <summary>
		/// Parses the RDF at the given URI
		/// </summary>
		/// <param name="uri">The Uri of the document to parse</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Uri uri)
		{
			return ParseRdf(uri,null);
		}


		/// <summary>
		/// Parses the RDF from the given stream, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="inStream">The Stream to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Stream inStream, IRdfGraph graph, string xmlbaseUri)
		{
			//looks for xml:base in rdf doc.
			//if not found, then uses xmlbaseUri
			//if xmlbaseUri is not a valid Uri it defaults to http://unknown.org/
			if(inStream == null)
				throw(new ArgumentNullException("The specified input stream is a null reference"));
			
			XmlDocument doc = new XmlDocument();
			try
			{
				doc.Load(inStream);
			}
			catch(XmlException xe)
			{
				OnError(xe);
				return null;
			}
			catch(Exception e)
			{
				OnError(e);
				return null;
			}
			return ParseRdf(doc, graph, xmlbaseUri);
		}

		/// <summary>
		/// Parses the RDF from the given stream, using the given xml:base uri
		/// </summary>
		/// <param name="inStream">The Stream to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Stream inStream, string xmlbaseUri)
		{
			return ParseRdf(inStream,null,xmlbaseUri);
		}
		
		/// <summary>
		/// Parses the RDF from a stream into an existing Graph
		/// </summary>
		/// <param name="inStream">The input stream for data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface that will be used as the destination graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Stream inStream, IRdfGraph graph)
		{
			return ParseRdf(inStream,graph,null);
		}

		/// <summary>
		/// Parses the RDF from a stream
		/// </summary>
		/// <param name="inStream">The input stream for data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(Stream inStream)
		{
			//looks for xml:base in rdf doc.
			//if not found, then defaults to http://unknown.org/
			return ParseRdf(inStream, null, null);
		}

		/// <summary>
		/// Parses the RDF at the given URI, into an existing graph
		/// </summary>
		/// <param name="uri">The Uri of the document to parse</param>
		/// <param name="graph">An object that implements the IRdfGraph interface that will be used as the destination graph</param>
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
			return ParseRdf(srcUri,graph);
		}

		/// <summary>
		/// Builds an RdfGraph from an RDF/XML serialization
		/// </summary>
		/// <param name="uri">The URI of the RDF document to parse</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		public IRdfGraph ParseRdf(string uri)
		{
			return ParseRdf(uri,null);
		}
		
		/// <summary>
		/// Gets the value of the xml:base attribute from the XmlNode if one exists
		/// </summary>
		/// <param name="node">An Xml Node</param>
		/// <returns>A string containing the xml:base uri. Returns null if the xml:base attribute is not found</returns>
		private string GetXmlBase(XmlNode node)
		{
			Debug.Assert(node != null);
			XmlNode attr = node.Attributes["xml:base"];
			if(attr == null)
				return null;
			string xmlBaseUri =	attr.Value;
			if(!IsValidUri(xmlBaseUri))
				return null;

			//ignore and discard everything after the first # character
			int pos = xmlBaseUri.IndexOf('#');
			if(pos != -1)
			{
				xmlBaseUri = xmlBaseUri.Remove(pos,xmlBaseUri.Length-pos);
			}
			return xmlBaseUri;
		}

		/// <summary>
		/// Moves all the edges associated with the source node to the destination node
		/// </summary>
		/// <param name="srcNode">The node from which the edges are to be moved.</param>
		/// <param name="destNode">The node to which the edges are to be moved</param>
		/// <remarks>This method moves all the edges from the src node to the dest node. 
		/// The src node is not removed from the graph. </remarks>
		private void MoveEdges(RdfNode srcNode, RdfNode destNode)
		{
			Debug.Assert(srcNode != null);
			Debug.Assert(destNode != null);
			foreach(IRdfEdge parentEdge in srcNode.ParentEdges)
			{
				parentEdge.AttachChildNode(destNode);
			}
			foreach(IRdfEdge childEdge in srcNode.ChildEdges)
			{
				childEdge.AttachParentNode(destNode);
			}
		}

		/// <summary>
		/// Adds an RDF Container of type rdf:Alt to the graph</summary>
		/// <param name="nodeUri">The Uri of the container.</param>
		/// <returns>Returns a reference to the newly added container.</returns>
		/// <exception cref="UriFormatException">The specified nodeUri  is not a well formed Uri.</exception>
		/// <remarks>If a node with the specified Uri exists then it is converted to an RdfAlt (if required) and returned. 
		/// If the container does not exist a new one is created</remarks>
		private RdfAlt AddAltToGraph(string nodeUri)
		{
			//if the uri is null then create a blank node uri
			if(nodeUri == null)
				nodeUri = GetBlankNodeUri(null);
			RdfNode node = (RdfNode)_rdfGraph[nodeUri];
			if((node != null) && (node is RdfAlt))
				return (RdfAlt)node;
			RdfNode typeNode = AddNodeToGraph(RdfNamespaceCollection.RdfNamespace+"Alt");

			if(node == null)
			{
				node = new RdfAlt(nodeUri,typeNode);
				//add the type edge of the new Alt to the Graph
				AddEdgeToGraph(((RdfAlt)node).Type);
				_rdfGraph.AddNode(node);
				return (RdfAlt)node;
			}

			RdfAlt newNode = new RdfAlt(nodeUri,typeNode);
			//add the type edge of the new Alt to the Graph
			AddEdgeToGraph(newNode.Type);
			MoveEdges(node, newNode);
			_rdfGraph.Nodes.Remove(node);
			_rdfGraph.AddNode(newNode);
			return newNode;
		}

		/// <summary>
		/// Adds an RdfStatement to the RDF graph
		/// </summary>
		/// <param name="statementUri">The Uri of the statement.</param>
		/// <exception cref="UriFormatException">The specified statementUri is not a well formed Uri.</exception>
		/// <returns>A reference to the newly added statement</returns>
		/// <remarks>If a node with the specified Uri exists then it is converted to an RdfStatement (if required) and returned. 
		/// If the statement does not exist a new one is created</remarks>
		private RdfStatement AddStatementToGraph(string statementUri)
		{
			RdfNode node = (RdfNode)_rdfGraph[statementUri];
			if((node != null) && (node is RdfStatement))
				return (RdfStatement)node;
			RdfNode typeNode = AddNodeToGraph(RdfNamespaceCollection.RdfNamespace+"Statement");

			RdfStatement stNode = new RdfStatement(statementUri,typeNode);
			//add the type edge for this statement to the graph
			AddEdgeToGraph(stNode.Type);
			AddEdgeToGraph(stNode.RdfSubject);
			AddEdgeToGraph(stNode.RdfPredicate);
			AddEdgeToGraph(stNode.RdfObject);
			_rdfGraph.AddNode(stNode);

			if(node != null)
			{
				MoveEdges(node, stNode);
				_rdfGraph.Nodes.Remove(node);
			}

			return stNode;
		}

		/// <summary>
		/// Adds an RDF Container of type rdf:Bag to the graph</summary>
		/// <param name="nodeUri">The Uri of the container.</param>
		/// <returns>Returns a reference to the newly added container.</returns>
		/// <exception cref="UriFormatException">The specified nodeUri is not a well formed Uri.</exception>
		/// <remarks>If a node with the specified Uri exists then it is converted to an RdfBag (if required) and returned. 
		/// If the container does not exist a new one is created</remarks>
		private RdfBag AddBagToGraph(string nodeUri)
		{
			//if the usr is null then create a blank node uri
			if(nodeUri == null)
				nodeUri = GetBlankNodeUri(null);
			RdfNode node = (RdfNode)_rdfGraph[nodeUri];
			if((node != null) && (node is RdfBag))
				return (RdfBag)node;
			RdfNode typeNode = AddNodeToGraph(RdfNamespaceCollection.RdfNamespace+"Bag");
			if(node == null)
			{
				node = new RdfBag(nodeUri,typeNode);
				//add the type edge of the bag to the graph
				AddEdgeToGraph(((RdfBag)node).Type);
				_rdfGraph.AddNode(node);
				return (RdfBag)node;
			}
			RdfBag newNode = new RdfBag(nodeUri, typeNode);
			//add the type edge of the bag to the graph
			AddEdgeToGraph(newNode.Type);
			MoveEdges(node, newNode);
			_rdfGraph.Nodes.Remove(node);
			_rdfGraph.AddNode(newNode);
			return newNode;
		}

		/// <summary>
		/// Adds an RDF Container of type rdf:Seq to the graph</summary>
		/// <param name="nodeUri">The Uri of the container.</param>
		/// <returns>Returns a reference to the newly added container.</returns>
		/// <exception cref="UriFormatException">The specified nodeUri  is not a well formed Uri.</exception>
		/// <remarks>If a node with the specified Uri exists then it is converted to an RdfSeq (if required) and returned. 
		/// If the container does not exist a new one is created</remarks>
		private RdfSeq AddSeqToGraph(string nodeUri)
		{
			//if the usr is null then create a blank node uri
			if(nodeUri == null)
				nodeUri = GetBlankNodeUri(null);
			RdfNode node = (RdfNode)_rdfGraph[nodeUri];
			if((node != null) && (node is RdfSeq))
				return (RdfSeq)node;
			RdfNode typeNode = AddNodeToGraph(RdfNamespaceCollection.RdfNamespace+"Seq");

			if(node == null)
			{
				node = new RdfSeq(nodeUri,typeNode);
				//add the type edge of the new Seq to the graph
				AddEdgeToGraph(((RdfSeq)node).Type);
				_rdfGraph.AddNode(node);
				return (RdfSeq)node;
			}

			RdfSeq newNode = new RdfSeq(nodeUri, typeNode);
			//add the type edge of the new Seq to the graph
			AddEdgeToGraph(newNode.Type);
			
			MoveEdges(node, newNode);
			_rdfGraph.Nodes.Remove(node);
			_rdfGraph.AddNode(newNode);
			return newNode;
		}

		/// <summary>
		/// Creates a typed node with the specified nodeUri
		/// </summary>
		/// <param name="node">The XmlNode element specifying the type of the RdfNode</param>
		/// <param name="nodeUri">The Uri of the new node. It may be null, blank or a relative Uri.</param>
		/// <returns>A reference to the newly created TypedNode</returns>
		/// <exception cref="ArgumentNullException">node  is null a null reference</exception>
		/// <remarks>The new node created has a child edge with ID rdf:type from this node pointing 
		/// to a node with URI specified by the name of the given XmlNode element.
		/// If the specified URI is null or empty a new blank node Uri is created. 
		/// If the Uri is a relative URI it is converted to an absolute URI by prefixing it with the value given by xml:base.</remarks>
		private RdfNode CreateTypedNode(XmlNode node, string nodeUri)
		{
			Debug.Assert(node != null);
			String namespaceUri = node.NamespaceURI;
			String localName = node.LocalName;

			if((nodeUri == null) || (nodeUri.Length == 0))
				nodeUri = GetBlankNodeUri(null);
			else
				nodeUri = PrependXmlBase(nodeUri,GetXmlBase(node));
			//add the typed node to the graph
			RdfNode rNode = AddNodeToGraph(nodeUri);
			
			string childNodeUri = namespaceUri+localName;
			if((childNodeUri == null) || (childNodeUri.Length == 0))
				childNodeUri = GetBlankNodeUri(null);
			else
				childNodeUri = PrependXmlBase(childNodeUri, GetXmlBase(node));

			//add the child node of the typedNode i.e. the node that indicates the type
			RdfNode childNode = AddNodeToGraph(childNodeUri);
			
			//create an rdf:type edge
			RdfEdge rEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			
			//add the edge to the graph
			AddEdgeToGraph(rEdge);
			//connect all three
			rNode.AttachChildEdge(rEdge);
			rEdge.AttachChildNode(childNode);
			
			return rNode;
		}
	
		/// <summary>
		/// Processes each node in the XML document. 
		/// </summary>
		/// <param name="node">The XmlNode to process.</param>
		/// <param name="bIsRdfNode">Indicates if we are about to process a node or an edge in the RDF Graph</param>
		/// <param name="parent">The parent of the current node.</param>
		/// <returns>Returns a reference to the new node or edge created</returns>
		/// <exception cref="InvalidRdfException">The RDF Syntax encountered is invalid</exception>
		/// <remarks>This method is called recursively to build the RDF graph.
		/// The parent is an object of type RdfNode or RdfEdge.
		/// An RdfParseException is thrown to indicate a parser error.</remarks>
		private Object ProcessNode(XmlNode node, bool bIsRdfNode, Object parent)
		{
			//if the node is a comment or anything else then totally ignore
			if((node.NodeType == XmlNodeType.Comment) || (node.NodeType == XmlNodeType.None) || (node.NodeType == XmlNodeType.Whitespace) || (node.NodeType == XmlNodeType.SignificantWhitespace))
				return true;
			RdfNode rNode = null;
			RdfEdge rEdge = null;
			
			if(node.NodeType == XmlNodeType.Element)
			{
				//get the xml:base attribute...
				XmlAttribute xmlBaseAttr = node.Attributes["xml:base"];
				if((xmlBaseAttr == null) && (parent != null))
				{
					//ok the child does not have an xml:base... and the parent is not null i.e. this node is not a child of the rdf:RDF root
					xmlBaseAttr = node.ParentNode.Attributes["xml:base"];
					if(xmlBaseAttr != null)
						node.Attributes.Append(xmlBaseAttr);
				}
			}

			if(bIsRdfNode) //we are processing an Rdf node in the XML Dom tree. i.e. a node wrt to the striped RDF syntax
			{
				RdfEdge parentRdfEdge = null;
				string langID = null;
				if(parent != null)
				{
					parentRdfEdge = (RdfEdge)parent;
					langID = parentRdfEdge.LangID;
				}
				if(node.NodeType == XmlNodeType.Element)
				{
					//first parse the Rdf attributes rdf:about, rdf:ID, rdf:nodeID
					string nodeID = ParseRdfAttributes(node);

					rNode = ParseRdfSyntax(node,nodeID);
					if(rNode == null) 		//its not standard RDF Syntax so create an edge (rdf:type) from this node
						rNode = CreateTypedNode(node,nodeID);

					//try and get the xml:lang attribute or inherit the xml:lang attribute from the parent if applicable
					XmlNode langAttr = node.Attributes["xml:lang"];
					if(langAttr != null)
						rNode.LangID = langAttr.Value;
					else
						rNode.LangID = langID;

					//process the regular (Non-Rdf) attributes of this node
					ParseNodeAttributes(node, rNode);
				}
				else if((node.NodeType == XmlNodeType.Text) || (node.NodeType == XmlNodeType.CDATA))
				{
					//its a literal
					//so get the lang ID and the datatype from the parent node
					string datatypeUri = GetDatatypeUri(node.ParentNode);
					rNode = AddLiteralToGraph(node.Value,langID, datatypeUri);
				}

				if(parentRdfEdge != null)
					parentRdfEdge.AttachChildNode(rNode);

				if(node.HasChildNodes)
				{
					int count = node.ChildNodes.Count;
					for(int i=0;i<count;i++)
						ProcessNode(node.ChildNodes[i], !bIsRdfNode, rNode);
				}
			}
			else //we are processing an edge in the RDF Graph. i.e. an edge wrt to the Striped RDF Syntax
			{
				rEdge = new RdfEdge();
				Debug.Assert(parent != null);

				RdfNode parentRdfNode = (RdfNode)parent;
				//process the xml:lang attribute if applicable
				rEdge.LangID = parentRdfNode.LangID;
				
				if(!ParseRdfSyntax(node,rEdge, parentRdfNode))
				{
					rEdge.ID = node.NamespaceURI+node.LocalName;
					rEdge.AttachParentNode(parentRdfNode);
				}
				
				if(ParseRdfAttributes(node,rEdge)) //if the process attributes method returns true then process the children of this node
				{
					if(node.HasChildNodes)
					{
						int count = node.ChildNodes.Count;
						for(int i=0;i<count;i++)
							ProcessNode(node.ChildNodes[i], !bIsRdfNode, rEdge);
					}
				}

				//coming back up the tree
				ParseEdgeAttributes(node,rEdge);
				//if the edge is dangling then put an empty Literal on it.
				if(rEdge.ChildNode == null)
					rEdge.AttachChildNode(AddLiteralToGraph("",null,null));

				//Now process the rdf:ID attributes to complete reification 
				ParseReificationAttributes(node, rEdge);

				//add the edge to the Graph
				AddEdgeToGraph(rEdge); 
			}
			if(bIsRdfNode)
				return rNode;
			else
				return rEdge;
		}

		/// <summary>
		/// Gets the rdf:datatype Uri from the specified XML element
		/// </summary>
		/// <param name="node">The XmlNode from which to extract the rdf:datatype attribute</param>
		/// <returns>A string representing the datatype Uri. A null reference is returned if the rdf:datatype attribute is not found.</returns>
		/// <remarks>This method looks for the rdf:datatype attribute on the specified XML element and returns the value.</remarks>
		private string GetDatatypeUri(XmlNode node)
		{
			XmlNode datatypeAttr = node.Attributes["rdf:datatype"];
			if(datatypeAttr != null)
				return PrependXmlBase(datatypeAttr.Value, GetXmlBase(node));
			return null;
		}

		/// <summary>
		/// Determines whether the XML Node is a property that is part of the RDF or XML syntax
		/// </summary>
		/// <param name="prop">An XmlNode</param>
		/// <returns>True if the property is part of the RDF or XML syntax or if the property is reserved for use by xml</returns>
		/// <remarks>This method returns true is the property localname or prefix begins with xml (regardless of whether xml is in uppercase, lowercase or
		/// any combination thereof. </remarks>
		private bool IsRdfXmlProperty(XmlNode prop)
		{
			if(_rdfXmlProperties[prop.Name] != null)
				return true;
			if((prop.Prefix.ToLower().StartsWith("xml")) || (prop.LocalName.ToLower().StartsWith("xml")))
				return true;
			if((prop.Prefix == "rdf") && IsListMember(prop.LocalName))
				return true;
			if(((prop.Prefix == "rdf") || (prop.Prefix == "xml")) && (!IsNonSyntacticRdfElement(prop) && (!IsSyntacticRdfElement(prop))))
				OnWarning("Unknown RDF or XML property: "+prop.Prefix+":"+prop.LocalName);
			return false;
		}

		/// <summary>
		/// Determines whether the XML Node is a non syntactix RDF element
		/// </summary>
		/// <param name="prop">An XmlNode</param>
		/// <returns>True if the element is a non syntactic RDF element.</returns>
		/// <remarks>The non syntactic RDF elements are 
		/// rdf:Alt rdf:Seq rdf:Bag rdf:rest rdf:first rdf:predicate rdf:object rdf:List rdf:subject rdf:Statement rdf:Property</remarks>
		private bool IsNonSyntacticRdfElement(XmlNode prop)
		{
			if(_nonSyntacticRdfElements[prop.Name] != null)
				return true;
			return false;
		}

		/// <summary>
		/// Determines whether the XML Node is a syntactix RDF element
		/// </summary>
		/// <param name="node">An XmlNode</param>
		/// <returns>True if the element is a non syntactic RDF element.</returns>
		/// <remarks>The syntactic RDF elements are rdf:RDF, rdf:ID, rdf:about, rdf:resource, rdf:parseType, rdf:li</remarks>
		private bool IsSyntacticRdfElement(XmlNode node)
		{
			if(_syntacticRdfElements[node.Name] != null)
				return true;
			return false;
		}

		/// <summary>
		/// Determines whether the XML Node is an unqualified RDF property
		/// </summary>
		/// <param name="prop"></param>
		/// <returns></returns>
		private bool IsUnqualifiedRdfProperty(XmlNode prop)
		{
			string prefix = prop.Prefix;
			string localName = prop.LocalName;

			if(prefix.Length == 0)
			{
				if(_rdfXmlProperties["rdf:"+localName] != null)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Parses the reification attribute rdf:ID as it appears on an edge in the RDF graph.
		/// </summary>
		/// <param name="node">The XmlNode containing the attributes</param>
		/// <param name="rEdge">The predicate if the rdf:ID attribute is found and the parent of the subject node if the rdf:bagID attribut is found.</param>
		/// <remarks>Reifies the triple with predicate rEdge if the attribute rdf:ID is found and 
		/// creates a bag of reified statements if the rdf:bagID attribute is found</remarks>
		private void ParseReificationAttributes(XmlNode node, RdfEdge rEdge)
		{
			XmlNode attr = node.Attributes["rdf:ID"];
			if(attr != null)
			{
				RdfNode predicate = AddNodeToGraph(rEdge.ID);
				string statementID = attr.Value;
				if(!IsXmlName(statementID))
					OnError(statementID+" is not an XML name");
				if((statementID == null) || (statementID.Length == 0))
					statementID = GetBlankNodeUri(null);
				else
					statementID = PrependXmlBase(attr.Value,GetXmlBase(node));
				CreateReifiedStatement(statementID,(RdfNode)rEdge.ParentNode,predicate,(RdfNode)rEdge.ChildNode);
			}
		}

		/// <summary>
		/// Parses the rdf:value, rdf:type attributes as well as any attributes not part of the RDF or XML namespace
		/// </summary>
		/// <param name="node">The XmlNode on which the attributes appear</param>
		/// <param name="rNode">The RdfNode to which the attributes must be applied</param>
		private void ParseNodeAttributes(XmlNode node, RdfNode rNode)
		{
			int count = node.Attributes.Count;
			for(int i=0;i<count;i++)
			{
				XmlAttribute attr = node.Attributes[i];
				if(!IsRdfXmlProperty(attr) || (attr.Name=="rdf:type") || (attr.Name == "rdf:value") || ((attr.Prefix == "rdf") && IsListMember(attr.LocalName)))
				{
					if(IsUnqualifiedRdfProperty(node.Attributes[i]))
						OnWarning("Unqualified use of rdf:"+node.Attributes[i].LocalName);

					//special case rdf:li is not allowed as an attribute
					if(attr.Name == "rdf:li")
						OnError("rdf:li is not allowed as an attribute");
					//create a new edge
					string edgeUri = PrependXmlBase(attr.NamespaceURI+attr.LocalName, GetXmlBase(node));
					RdfEdge rEdge = new RdfEdge(edgeUri);
					RdfNode childNode = null;
					if(attr.Name == "rdf:type")
					{
						childNode = AddNodeToGraph(PrependXmlBase(attr.Value,GetXmlBase(node)));
					}
					else
					{
						childNode = AddLiteralToGraph(attr.Value,rNode.LangID,GetDatatypeUri(node));
					}
					Debug.Assert(childNode != null);
					
					rNode.AttachChildEdge(rEdge);
					rEdge.AttachChildNode(childNode);
					//add the new edge to the graph
					AddEdgeToGraph(rEdge);
				}
			}
		}

		/// <summary>
		/// Process the rdf:value and rdf:_n attributes and any attributes not in the rdf or xml namespace
		/// </summary>
		/// <param name="node">The XmlNode that attributes appear on</param>
		/// <param name="rEdge">The RDF edge that the attributes must be applied to.</param>
		private void ParseEdgeAttributes(XmlNode node, RdfEdge rEdge)
		{
			//go through all the attributes
			int count = node.Attributes.Count;
			for(int i=0;i<count;i++)
			{
				XmlAttribute attr = node.Attributes[i];
				
				if((!IsRdfXmlProperty(attr))  || (attr.Prefix == "rdf" && (attr.LocalName == "value" || IsListMember(attr.LocalName))))
				{
					if((attr.NamespaceURI == null) || (attr.NamespaceURI.Length == 0))
					{
						OnError("Unqualified attribute: "+attr.LocalName);
						continue;
					}
					if((attr.Name == "rdf:value")&& (rEdge.ChildNode is RdfLiteral))
					{
						OnError("Cannot use rdf:value ("+attr.Value+") as property for a literal ("+((RdfLiteral)rEdge.ChildNode).Value+").");
						continue;
					}
					//if the childnode of the edge is a literal then it cant have any arcs going out from it
					if((rEdge.ChildNode != null) && (rEdge.ChildNode is RdfLiteral))
					{
						OnError("Cannot have proerty "+attr.Name+" for an Rdf Literal");
						continue;
					}
					//special case rdf:li is not allowed as an attribute
					if(attr.Name == "rdf:li")
						OnError("rdf:li is not allowed as an attribute");

					string edgeID = attr.NamespaceURI+attr.LocalName;
					string literalValue = attr.Value;
					string langID = rEdge.LangID;
					string datatypeUri = GetDatatypeUri(node);

					//if this edge does not have a child node then create a blank node and add it
					if(rEdge.ChildNode == null)
						rEdge.AttachChildNode(AddNodeToGraph(GetBlankNodeUri(null)));

					//make an edge from the child of rEdge
					RdfEdge childEdge = new RdfEdge();
					childEdge.ID = edgeID;
					RdfLiteral childLiteral = AddLiteralToGraph(literalValue,langID,datatypeUri);
		
					rEdge.ChildNode.AttachChildEdge(childEdge);
					//attach it to the edge
					childEdge.AttachChildNode(childLiteral);
					//add the edge to the graph
					AddEdgeToGraph(childEdge);
				}
			}
		}
		
		/// <summary>
		/// Parses the RDF attributes rdf:about, rdf:ID and rdf:nodeID
		/// </summary>
		/// <param name="node">The XmlNode on which the attributes appear</param>
		/// <returns>A Uri string with the ID specified by the attributes. Null if none of the three attributes are found</returns>
		private string ParseRdfAttributes(XmlNode node)
		{
			int attrFound = 0;
			XmlNode attr = node.Attributes["rdf:about"];
			string retVal = null;
			if(attr != null)
			{
				//found an rdf:about attribute
				retVal = QualifyResource(attr.Value, GetXmlBase(node));
				attrFound = 1;
			}
			attr = node.Attributes["rdf:ID"];
			if(attr != null)
			{
				//found an rdf:ID attribute
				//check that its a valid xml name
				if(!IsXmlName(attr.Value))
					OnError(attr.Value+" is not an XML Name");
				//now check if its already been declared
				if(_declID[attr.Value] != null)
					OnError("Redefinition of rdf:ID "+attr.Value);
				else
					_declID[attr.Value] = attr.Value;
				retVal = PrependXmlBase(attr.Value, GetXmlBase(node));
				attrFound += 2;
			}
			attr = node.Attributes["rdf:nodeID"];
			if(attr != null)
			{
				//found an rdf:nodeID attribute
				//check if its an xml name
				if(!IsXmlName(attr.Value))
					OnError(attr.Value+" is not an XML Name");
				retVal = GetBlankNodeUri(attr.Value);
				attrFound += 4;
			}
			switch(attrFound)
			{
				case 3:
					OnError("Cannot use rdf:about and rdf:ID together");
					break;
				case 5:
					OnError("Cannot use rdf:about and rdf:nodeID together");
					break;
				case 6:
					OnError("Cannot use rdf:ID and rdf:nodeID together");
					break;
				case 7:
					OnError("Cannot use rdf:about, rdf:ID and rdf:nodeID together");
					break;
			}
			return retVal;
		}

		/// <summary>
		/// Parses the RDF Attributes rdf:resource, rdf:nodeID, rdf:parseType, and xml:lang as they appear on an edge
		/// </summary>
		/// <param name="node">The XmlNode on which the attributes appear</param>
		/// <param name="rEdge">The RdfEdge to which the attributes must be applied</param>
		/// <returns>True if the children of the specified XML Node should be parsed</returns>
		private bool ParseRdfAttributes(XmlNode node, RdfEdge rEdge)
		{
			int attrFound = 0;
			bool parseChildren = true;
			XmlNode attr = node.Attributes["rdf:resource"];
			if(attr != null)
			{
				ProcessRdfResource(node, rEdge, attr.Value);
				attrFound = 1;
				parseChildren = false;
			}
			attr = node.Attributes["rdf:nodeID"];
			if(attr != null)
			{
				ProcessRdfNodeID(rEdge, attr.Value);
				attrFound += 2;
				parseChildren = false;
			}
			attr = node.Attributes["rdf:parseType"];
			if(attr != null)
			{
				ProcessRdfParseType(node,rEdge,attr.Value);
				attrFound += 4;
				parseChildren = false;
			}
			attr = node.Attributes["xml:lang"];
			if(attr != null)
			{
				rEdge.LangID = attr.Value;
			}
			switch(attrFound)
			{
				case 3:
					OnError("Cannot use rdf:resource and rdf:nodeID together");
					break;
				case 5:
					OnError("Cannot use rdf:resource and rdf:parseType together");
					break;
				case 6:
					OnError("Cannot use rdf:nodeID and rdf:parseType together");
					break;
				case 7:
					OnError("Cannot use rdf:resource, rdf:nodeID and rdf:parseType together");
					break;
			}
			return parseChildren;
		}

		/// <summary>
		/// Gets a URI string for a new blank node
		/// </summary>
		/// <param name="baseID">The base ID from which the Uri must be created.</param>
		/// <returns>A string containing a well formed URI</returns>
		private string GetBlankNodeUri(string baseID)
		{
			if((baseID == null) || (baseID.Length == 0))
				return "blankID:"+_newID++;
			else
				return "blankID:"+baseID;
		}

		/// <summary>
		/// Process the rdf:nodeID attribute found on the RDF edge
		/// </summary>
		/// <param name="rEdge">The edge to which the rdf:nodeID attribute must be applied</param>
		/// <param name="baseNodeID">The ID specified by the rdf:nodeID attribute</param>
		private void ProcessRdfNodeID(RdfEdge rEdge, string baseNodeID)
		{
			if(!IsXmlName(baseNodeID))
				OnError(baseNodeID+" is not an XML name");
			RdfNode rNode = AddNodeToGraph(GetBlankNodeUri(baseNodeID));
			rEdge.AttachChildNode(rNode);
		}

		/// <summary>
		/// Create a node from the rdf:resource attribute and attach it to a given edge as a child
		/// </summary>
		/// <param name="node">The XmlNode that contains the rdf:resource attribute.</param>
		/// <param name="rEdge">The edge to which the new childnode must be added.</param>
		/// <param name="resourceUri">The URI specified by the rdf:resource attribute.</param>
		/// <remarks>If the specified Uri is null or empty a new blank node URI is created. 
		/// If it is a relative URI then it is converted to an absolute URI by prefixing it with the value given by xml:base</remarks>
		private void ProcessRdfResource(XmlNode node, RdfEdge rEdge, string resourceUri)
		{
			string nodeID = QualifyResource(resourceUri,GetXmlBase(node));
			RdfNode rNode = AddNodeToGraph(nodeID);
			rEdge.AttachChildNode(rNode);
		}

		/// <summary>
		/// Converts the value given by rdf:about into a fully qualified Uri
		/// </summary>
		/// <param name="val">The value specified by the rdf:about attribute</param>
		/// <param name="xmlBaseUriString">An string specifying an xml:base Uri to use. This parameter can be null.</param>
		/// <returns>A well formed Uri string</returns>
		/// <remarks>This method should only be used to convert rdf:about and rdf:resource values into fully qualified URIs. 
		/// If the xmlBaseUriString is null or an empty string then the global value for xml:base will be used.</remarks>
		private string QualifyResource(string val, string xmlBaseUriString)
		{
			if((xmlBaseUriString == null) || (xmlBaseUriString.Length == 0))
				xmlBaseUriString = _rdfGraph.NameSpaces["xml:base"];
			Debug.Assert(xmlBaseUriString != null);
			Debug.Assert(xmlBaseUriString.Length != 0);
			//if val is blank or null, val = xml:base
			if((val == null) || (val.Length == 0))
				return xmlBaseUriString;

			Uri xmlBaseUri = new Uri(xmlBaseUriString);

			// if val starts with // or val starts with \\
			// val = scheme + ":"+ val;
			if(val.StartsWith("\\\\") || val.StartsWith("//"))
				return xmlBaseUri.Scheme+":"+val;
			if(IsValidUri(val))
				return val;

			// if val starts with #
			// val = xml:base+val
			if(val.StartsWith("#"))
				return xmlBaseUriString+val;

			// if val starts with /
			// val = scheme + "://" + authority + val;
			if(val.StartsWith("/") || val.StartsWith("\\"))
				return xmlBaseUri.Scheme+"://"+xmlBaseUri.Authority+val;

			string folderPath = GetFolderPath(xmlBaseUri.AbsolutePath);
			// if val starts with ../
			// val = scheme+"://" + authority + modpath {modpath = GetAbsolutePath(folderpath,val)}
			if(val.StartsWith("../") || val.StartsWith("..\\"))
			{
				string absPath = GetAbsolutePath(folderPath, val);
				return xmlBaseUri.Scheme+"://"+xmlBaseUri.Authority+absPath;
			}
			// if val doesnt start with anything 
			// val = scheme+"://" + authority + folderPath + val
			return xmlBaseUri.Scheme+"://"+xmlBaseUri.Authority+folderPath+val;
		}


		private string GetFolderPath(string pathStr)
		{
			Debug.Assert(pathStr != null);
			if(!(pathStr.EndsWith("/") || pathStr.EndsWith("\\")))
			{
				int index = pathStr.LastIndexOfAny(new Char[]{'\\','/'});
				Debug.Assert(index != -1);
				pathStr = pathStr.Substring(0,index+1);
			}
			return pathStr;
		}

		/// <summary>
		/// Returns an absolute path by combining the initial path and a relative path
		/// </summary>
		/// <param name="initPath">A string representing the initial path</param>
		/// <param name="relPath">A string representing the relative path</param>
		/// <returns>A string representing the absolute path formed by the combination of the initial and relative paths.</returns>
		/// <remarks>The init path should be terminated at both ends by / or \ characters. This method concatenates the initial 
		/// and relative paths and returns an absolute path with proper handling of ../ prefixes on the relative path</remarks>
		private string GetAbsolutePath(string initPath, string relPath)
		{
			Debug.Assert(initPath != null);
			Debug.Assert(relPath != null);
			Debug.Assert(initPath.StartsWith("\\") || initPath.StartsWith("/"));
			Debug.Assert(initPath.EndsWith("\\") || initPath.EndsWith("/"));

			Regex regex = new Regex(@"[/\\](\w)*[/\\](\.\.)[/\\]",RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
			string path = initPath+relPath;
			string oldPath = "";
			while(oldPath!= path)
			{
				oldPath = path;
				path = regex.Replace(oldPath,"/");
			}
			return path;
		}

		/// <summary>
		/// Makes a URI string from the specified ID by prepending the uri specified by xml:base to it
		/// </summary>
		/// <param name="id">The ID to convert to a URI string</param>
		/// <param name="xmlBaseUri">A string containing an xml:base Uri to use rather than the global xml:base Uri.</param>
		/// <remarks>This method checks the given xmlBaseUri string for the value of the xml:base URI to prepend to the ID.
		/// If it is null or an empty string then the global xml:base is used.</remarks>
		/// <returns>A string containing a well formed URI.</returns>
		private string PrependXmlBase(string id, string xmlBaseUri)
		{
			if(IsValidUri(id))
				return id;
			if(id == null)
				id = "";
			if((xmlBaseUri == null) || (xmlBaseUri.Length == 0))
				xmlBaseUri = _rdfGraph.NameSpaces["xml:base"];
			Debug.Assert(xmlBaseUri != null);
			if(id[0] == '#')
				id = xmlBaseUri+id;
			else
				id = xmlBaseUri+"#"+id;
			return id;
		}

		/// <summary>
		/// Processes the rdf:parseType attribute
		/// </summary>
		/// <param name="node">The XML Node on which the attribute appears</param>
		/// <param name="rEdge">The edge to which the attribute must be applied</param>
		/// <param name="parseType">The parse type as specified by the value of the attribute</param>
		private void ProcessRdfParseType(XmlNode node, RdfEdge rEdge, string parseType)
		{
			if(parseType == "Resource")
			{
				//its an rdf:parseType="Resource" so we must now parse its children
				//the children of this xmlNode are now EDGES in the RDF graph that 
				//extend out from the dest node of this edge i.e. the rdfNode created above
							
				//create a new node that will be the child of this edge
				//create a new node ID
				RdfNode rNode = AddNodeToGraph(GetBlankNodeUri(null));
				//attach this node to the edge
				rEdge.AttachChildNode(rNode);
				if(node.HasChildNodes)
				{
					int count = node.ChildNodes.Count;
					for(int i=0;i<count;i++)
						ProcessNode(node.ChildNodes[i], false, rNode);
				}
			}
			else if(parseType == "Literal")
			{
				//its an rdf:parseType="Literal" so all the xml below this node 
				//will be the content of the dest node i.e. rdfNode
				//create a new node that will be the child of this edge
				//create a new node ID
				string literalValue = node.InnerXml;
				string datatypeUri = GetDatatypeUri(node);
				if(datatypeUri == null)
					datatypeUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#XMLLiteral";
				RdfLiteral literal = AddLiteralToGraph(literalValue,rEdge.LangID,datatypeUri);
				//attach this node to the edge
				rEdge.AttachChildNode(literal);
			}
			else if(parseType == "Collection")
			{
				//its a Collection so make a cons list
				//get the children of this node
				RdfNode rNode = BuildCollection(node);
				//connect the collection to the edge
				rEdge.AttachChildNode(rNode);
			}
			else
			{
				OnError("Unknown parseType "+parseType);
			}
		}

		/// <summary>
		/// Adds an edge and a child node to the specified parent node 
		/// </summary>
		/// <param name="parentNode">The node to which the edge should be added</param>
		/// <param name="rdfType">The type of the node. This param is prefaced with the rdf Uri to convert it to an absolute Uri</param>
		private void AddTypeStatement(RdfNode parentNode, string rdfType)
		{
			if(parentNode == null)
				return;
			
			//create the typeEdge
			RdfEdge typeEdge = new RdfEdge();
			typeEdge.ID = RdfNamespaceCollection.RdfNamespace+"type";
			//add the typeEdge to the Graph
			AddEdgeToGraph(typeEdge);
			//find/create the typeNode
			RdfNode typeNode = AddNodeToGraph(RdfNamespaceCollection.RdfNamespace+rdfType);
			
			//connect all three
			parentNode.AttachChildEdge(typeEdge);
			typeEdge.AttachChildNode(typeNode);
		}

		/// <summary>
		/// Builds an RDF Collection.
		/// </summary>
		/// <param name="propertyNode">The XML Node containing the children that will form the members of this list</param>
		/// <returns>The head of the collection. This is the first member of the list or a nil node if the list is empty</returns>
		private RdfNode BuildCollection(XmlNode propertyNode)
		{
			RdfNode rNode;
			RdfNode collHead = null;
			RdfEdge restEdge = null;
			RdfEdge firstEdge;
			int count = propertyNode.ChildNodes.Count;
			for(int i=0;i<count;i++)
			{
				//build the blank node that is a member of this list
				rNode = AddNodeToGraph(GetBlankNodeUri(null));
				//if this is the first element of the cons list then set this as the head of the collection so we may return it later
				if(i == 0)
					collHead = rNode;

				//add an outgoing edge with name rdf:type from rdfNode to rdf:List
				AddTypeStatement(rNode, "List");
										
				//create a new first edge
				firstEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"first");
				rNode.AttachChildEdge(firstEdge);
				//add the first edge to the graph
				AddEdgeToGraph(firstEdge);
				RdfNode firstNode =	(RdfNode)ProcessNode(propertyNode.ChildNodes[i],true,firstEdge);
				firstEdge.AttachChildNode(firstNode);
		
				if(restEdge != null)
					rNode.AttachParentEdge(restEdge);

				//make the new rest Edge
				restEdge = new RdfEdge();
				restEdge.ID = RdfNamespaceCollection.RdfNamespace+"rest";
				restEdge.AttachParentNode(rNode);
				//add the new RestEdge to teh graph
				AddEdgeToGraph(restEdge);
			}
			//ok we have exited the loop or bypassed it because there are no children
			
			rNode = AddNodeToGraph(RdfNamespaceCollection.RdfNamespace+"nil");
			if(restEdge != null)
				rNode.AttachParentEdge(restEdge);
					
			if(collHead == null)
				collHead = rNode;
			return collHead;
		}
		
		/// <summary>
		/// Determines whether the specified name is an XML name
		/// </summary>
		/// <param name="name">A Name</param>
		/// <returns>True if the name is an XML name</returns>
		private bool IsXmlName(string name)
		{
			try
			{
				XmlConvert.VerifyNCName(name);
				return true;
			}
			catch(XmlException)
			{
				return false;
			}
		}

		/// <summary>
		/// Creates a Reified RDF statement
		/// </summary>
		/// <param name="statementID">The ID of the statement</param>
		/// <param name="subject">The subject of the statement</param>
		/// <param name="predicate">The predicate of Statement</param>
		/// <param name="obj">The Object of the statement</param>
		/// <returns>A reference to the newly created statement</returns>
		private RdfStatement CreateReifiedStatement(string statementID, RdfNode subject, RdfNode predicate, RdfNode obj)
		{
			Debug.Assert(IsValidUri(statementID));
			RdfStatement statement = AddStatementToGraph(statementID);

			//add the subject to the statement
			statement.RdfSubject.AttachChildNode(subject);

			//add the predicate to the statement
			statement.RdfPredicate.AttachChildNode(predicate);
			
			//add the object to the statement
			statement.RdfObject.AttachChildNode(obj);

			return statement;
		}

		/// <summary>
		/// Parses the RDF syntax [rdf:li, rdf:_n, rdf:type, rdf:value] on an edge.
		/// </summary>
		/// <param name="node">The XML Node representing the RDF edge</param>
		/// <param name="rEdge">The edge</param>
		/// <param name="parentNode">The parent RDF node</param>
		/// <returns>True if the edge was given an ID and attached to the parent node</returns>
		private bool ParseRdfSyntax(XmlNode node, RdfEdge rEdge, RdfNode parentNode)
		{
			//first get the NameSpace URI, NameSpace prefix and the LocalName for the node
			String nameSpaceURI = node.NamespaceURI;
			String nameSpacePrefix = node.Prefix;
			String localName = node.LocalName;
			
			if(nameSpaceURI == RdfNamespaceCollection.RdfNamespace)
			{
				if(localName=="li")						//process the rdf:li
				{
					//get the parent node ID
					//and get the ordinal of the last element
					object obj = _rdfLiTable[node.ParentNode];
					int ord = 0;
					if(obj != null)
						ord = (int)obj;

					//increment that and add the new stuff.
					ord++;
					rEdge.ID = RdfNamespaceCollection.RdfNamespace+"_"+ord;
					_rdfLiTable[node.ParentNode] = ord;
					rEdge.AttachParentNode(parentNode);
					return true;
				}
				if(localName == "type")					//process the rdf:type
				{
					rEdge.ID = RdfNamespaceCollection.RdfNamespace+"type";
					rEdge.AttachParentNode(parentNode);
					return true;
				}
				if(localName == "value")				//process the rdf:value
				{
					rEdge.ID = RdfNamespaceCollection.RdfNamespace+"value";
					rEdge.AttachParentNode(parentNode);
					return true;
				}
				if(IsListMember(localName))				//process the rdf:_n
				{
					rEdge.ID = RdfNamespaceCollection.RdfNamespace+localName;
					rEdge.AttachParentNode(parentNode);
					return true;
				}
				//None of the Syntactic RDF elements except rdf:li is allowed as a property element name
				if((node.Name != "rdf:li") && (IsSyntacticRdfElement(node)))
					OnError("Cannot use "+node.Name+" as a property element");
				if(!IsNonSyntacticRdfElement(node))
					OnWarning("Unknown Rdf property element rdf:"+localName);
				
			}
			return false;
		}

		/// <summary>
		/// Determines whether the edgename is a list member with format rdf:_n
		/// </summary>
		/// <param name="edgeName">The name of the edge</param>
		/// <returns>True if the name represents a list member</returns>
		private bool IsListMember(string edgeName)
		{
			if(edgeName.StartsWith("_"))
			{
				try
				{
					Int32.Parse(edgeName.Substring(1));
				}
				catch(FormatException)
				{
					return false;
				}
				return true;
			}
			return false;
		}

		/// <summary>
		/// Process the RDF Syntax [rdf:Description, rdf:Bag, rdf:Seq, rdf:Alt] on an RDF Node
		/// </summary>
		/// <param name="node">The XmlNode representing the RDF Node</param>
		/// <param name="nodeID">The ID to be assigned to the RDF node</param>
		/// <returns>The newly created RDF node or null if there are no RDF elements on this XmlNode</returns>
		private RdfNode ParseRdfSyntax(XmlNode node, string nodeID)
		{
			//first get the NameSpace URI, NameSpace prefix and the LocalName for the node
			String nameSpaceURI = node.NamespaceURI;
			String nameSpacePrefix = node.Prefix;
			String localName = node.LocalName;
			RdfNode rNode = null;
			//known elements in the RDF Syntax are rdf:Description, rdf:Bag, rdf:Seq, rdf:Alt
			if(nameSpaceURI==RdfNamespaceCollection.RdfNamespace)
			{
				if(localName=="Description")
				{
					if((nodeID == null) || (nodeID.Length == 0))
						nodeID = GetBlankNodeUri(null);
					else
						nodeID = PrependXmlBase(nodeID, GetXmlBase(node));
					rNode = AddNodeToGraph(nodeID);
					return rNode;
				}
				else if(localName=="Bag")
				{
					//its an rdf:Bag
					rNode = AddBagToGraph(nodeID);
					return rNode;
				}
				else if(localName=="Seq")
				{
					//its an rdf:Seq
					rNode = AddSeqToGraph(nodeID);
					return rNode;
				}
				else if(localName=="Alt")
				{
					//its an rdf:Alt
					rNode = AddAltToGraph(nodeID);
					return rNode;
				}
			}
			//apart from rdf:Description none of the syntactic RDF elements is allowed as a node element name
			//we don't have to check for rdf:Description because the first if statement takes care of it
			if(IsSyntacticRdfElement(node))
				OnError("Cannot use "+node.Name+" as a node element name");
			//print a warning if its an unknown rdf or xml property
			IsRdfXmlProperty(node);
				
			return rNode;
		}
	}
}
