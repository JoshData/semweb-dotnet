/*****************************************************************************
 * RdfGraph.cs
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
 * 
 ******************************************************************************/

using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents an RDF Graph. 
	/// </summary>
	/// <remarks>This is an implementation of the IRdfGraph interface. This class maintain a collection of Nodes and a separate collection of Litrerals
	/// in order to allow distinguishing between Nodes and Literals with the same value.</remarks>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfGraph : IRdfGraph
	{
		/// <summary>
		/// The namespaces associated with this RDF Graph
		/// </summary>
		private RdfNamespaceCollection _nameSpaces;

		/// <summary>
		/// Gets the namespaces associated with this RDF Graph
		/// </summary>
		public IRdfNamespaceCollection NameSpaces
		{
			get
			{
				return _nameSpaces;
			}
		}

		/// <summary>
		/// The collection of RdfEdges in this Graph
		/// </summary>
		private RdfEdgeCollection _edges;
		
		/// <summary>
		/// Gets the collection of RdfEdges in this Graph
		/// </summary>
		public IRdfEdgeCollection Edges
		{
			get
			{
				return (IRdfEdgeCollection)_edges;
			}
		}

		/// <summary>
		/// The collection of RdfNodes in this Graph
		/// </summary>
		private RdfNodeCollection _nodes;

		/// <summary>
		/// Gets the collection of RdfNodes in this Graph
		/// </summary>
		public IRdfNodeCollection Nodes
		{
			get
			{
				return (IRdfNodeCollection)_nodes;
			}
		}

		/// <summary>
		/// The collection of literals in this RDF Graph
		/// </summary>
		private RdfNodeCollection _literals;

		/// <summary>
		/// Gets the collection of literals in this RDF Graph
		/// </summary>
		public IRdfNodeCollection  Literals
		{
			get
			{
				return (IRdfNodeCollection)_literals;
			}
		}

		/// <summary>
		/// Gets the total number of nodes and literals in this RDF Graph
		/// </summary>
		public long Count
		{
			get
			{
				return _nodes.Count+_literals.Count;
			}
		}

		/// <summary>
		/// Adds a node to the Graph
		/// </summary>
		/// <param name="nodeUri">A string representing the URI of the new node.</param>
		/// <exception cref="UriFormatException">The specified nodeUri is not a well formed URI.</exception>
		/// <returns>An object that implements the IRdfNode interface. This is a reference to the new node added.
		/// This method checks the graph to determine whether the node with the specified URI exists. 
		/// If it does then a reference to the existing node is returned. If it does not exist then a new node is created, added 
		/// to the graph and returned.</returns>
		public IRdfNode AddNode(string nodeUri)
		{
			IRdfNode node = _nodes[nodeUri];
			if(node == null)
			{
				node = new RdfNode(nodeUri);
				_nodes[nodeUri] = node;
			}
			return node;
		}

		/// <summary>
		/// Adds a new node to the graph.
		/// </summary>
		/// <param name="node">An object that implements the IRdfNode interface. This is the new node to add.</param>
		/// <exception cref="ArgumentException">A node with the same ID already exists in the Graph.</exception>
		public void AddNode(IRdfNode node)
		{
			_nodes.Add((RdfNode)node);
		}

		/// <summary>
		/// Adds an edge to the graph
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		/// <exception cref="ArgumentNullException">The specified edge object is a null reference</exception>
		public void AddEdge(IRdfEdge edge)
		{
			_edges.Add(edge);
		}

		/// <summary>
		/// Adds a literal to the Graph
		/// </summary>
		/// <param name="literalValue">A string representing the value of the literal.</param>
		/// <returns>An object that implements the IRdfLiteral interface. This is a reference to the newly added literal.</returns>
		/// <remarks>This method looks in the graph to determine whether a literal with the specified value (and a null datatype and langID) exists
		/// in the Graph. If the literal exists a reference to the existing literal is returned. If it does not exist then a new literal (with the specified value, and null datatype and LangID)
		/// is created, added to the graph and returned.</remarks>
		public IRdfLiteral AddLiteral(string literalValue)
		{
			RdfLiteral literal = (RdfLiteral)_literals[literalValue];
			if(literal == null)
			{
				literal = new RdfLiteral(literalValue);
				_literals[literalValue] = literal;
			}
			return literal;
		}

		/// <summary>
		/// Adds a literal to the Graph
		/// </summary>
		/// <param name="literalValue">A string representing the value of the literal.</param>
		/// <param name="langID">A string representing the Language ID of the literal.</param>
		/// <param name="datatypeUri">A string representing the datatype URI of the literal.</param>
		/// <returns>An object that implements the IRdfLiteral interface. This is a reference to the newly added literal.</returns>
		/// <exception cref="UriFormatException">The specified datatype URI is not null and is not a well formed URI.</exception>
		/// <remarks>This method looks in the graph to determine whether a literal with the specified value, datatype and langID exists
		/// in the Graph. If the literal exists a reference to the existing literal is returned. If it does not exist then a new literal with the specified value, datatype and LangID
		/// is created, added to the graph and returned. Any parameter supplied to this method, except literalValue, can be null or empty and it will be ignored.</remarks>
		public IRdfLiteral AddLiteral(string literalValue, string langID, string datatypeUri)
		{
			string literalID = literalValue;
			if((langID != null) && (langID.Length != 0))
				literalID +="@"+langID;
			if((datatypeUri != null) && (datatypeUri.Length != 0))
				literalID +="^^"+datatypeUri;
			RdfLiteral literal = (RdfLiteral)_literals[literalID];
			if(literal == null)
			{
				literal = new RdfLiteral(literalValue,langID,datatypeUri);
				_literals[literalID] = literal;
			}
			return literal;
		}

		/// <summary>
		/// Adds a literal to the graph
		/// </summary>
		/// <param name="literal">The new literal to add.</param>
		/// <exception cref="ArgumentException">A literal with the same value, datatype URI and language ID alreday exists in the graph.</exception>
		public void AddLiteral(IRdfLiteral literal)
		{
			_literals.Add((RdfLiteral)literal);
		}

		/// <summary>
		/// Gets the node (or literal) with the specified URI
		/// </summary>
		/// <remarks>This method looks for a node that matches the specified URI and returns it. 
		/// If the node is not found then the first literal matching this URI (value+langiuageID+datatype URI) is returned.
		/// If neither a node or a literal matching this ID is found then null is returned.</remarks>
		public IRdfNode this[string nodeID]
		{
			get
			{
				IRdfNode node = _nodes[nodeID];
				if(node == null)
					node = _literals[nodeID];
				return node;
			}
		}

		/// <summary>
		/// Merges the srcGraph into this graph object
		/// </summary>
		/// <param name="srcGraph">An object that implements the IRdfGraph interace</param>
		/// <param name="skipDuplicateEdges">A flag that indicates whether duplicate edges present in both graphs should be skipped during the merge process.</param>
		public void Merge(IRdfGraph srcGraph, bool skipDuplicateEdges)
		{
			if(srcGraph == null)
				return;
			Hashtable literalsAdded = new Hashtable();
			//go through all the nodes in the source graph
			IDictionaryEnumerator enumerator = (IDictionaryEnumerator)srcGraph.Nodes.GetEnumerator();
			while(enumerator.MoveNext())
			{
				//Console.WriteLine(((IRdfNode)(enumerator.Value)).ID);
				//add this node to the graph
				IRdfNode srcParentNode = (IRdfNode)enumerator.Value;
				IRdfNode destParentNode = AddNode(srcParentNode.ID);
				//go through all of the src node's child edges
				foreach(IRdfEdge srcChildEdge in srcParentNode.ChildEdges)
				{
					//for each of the src node's child edges do...
					IRdfNode destChildNode;
					if(srcChildEdge.ChildNode is IRdfLiteral)
					{	
						IRdfLiteral srcChildLiteral = srcChildEdge.ChildNode as IRdfLiteral;
						Debug.Assert(srcChildLiteral != null);
						literalsAdded[srcChildLiteral] = srcChildLiteral;
						destChildNode = AddLiteral(srcChildLiteral.Value, srcChildLiteral.LangID, srcChildLiteral.Datatype);
					}
					else
					{
						destChildNode = AddNode(srcChildEdge.ChildNode.ID);
					}

					//Now we have the parent and the child nodes added to the graph..
					
					bool edgeExists = false;
					if(skipDuplicateEdges)
					{
						//does the new parent node and the new child node have an edge with the same ID as srcChildEdge?
						//go through all the child edges of destParentNode
						foreach(RdfEdge tempEdge in destParentNode.ChildEdges)
						{
							if((tempEdge.ChildNode == destChildNode) && (tempEdge.ID == srcChildEdge.ID))
							{
								edgeExists = true;
								break;
							}
						}
					}
					if(!skipDuplicateEdges || (skipDuplicateEdges && !edgeExists))
					{
						RdfEdge destChildEdge = new RdfEdge(srcChildEdge.ID);
						destParentNode.AttachChildEdge(destChildEdge);
						destChildEdge.AttachChildNode(destChildNode);
						//add the edge to the graph
						AddEdge(destChildEdge);
					}
				}
			}
		}

		/// <summary>
		/// Gets a collection of N-Triples represented by this graph
		/// </summary>
		/// <returns>An object that implements the IRdfNTripleCollection interface</returns>
		public IRdfNTripleCollection GetNTriples()
		{
			IRdfNTripleCollection triples = new RdfNTripleCollection();
			IDictionaryEnumerator enumerator = (IDictionaryEnumerator)Nodes.GetEnumerator();
			while(enumerator.MoveNext())
			{
				IRdfNode parentNode = (IRdfNode)enumerator.Value;
				foreach(IRdfEdge childEdge in parentNode.ChildEdges)
				{
					IRdfNTriple triple = new RdfNTriple();
					triple.Subject = parentNode.ToNTriple();
					triple.Predicate = childEdge.ToNTriple();
					triple.Object = childEdge.ChildNode.ToNTriple();
					triples.Add(triple);
				}
			}
			return triples;
		}

		/// <summary>
		/// Initializes a new instance of the RdfGraph class.
		/// </summary>
		public RdfGraph()
		{
			_nodes = new RdfNodeCollection();
			_literals = new RdfNodeCollection();
			_nameSpaces = new RdfNamespaceCollection();
			_edges = new RdfEdgeCollection();
		}
	}
}

