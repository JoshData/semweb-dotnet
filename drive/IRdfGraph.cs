/*****************************************************************************
 * IRfGraph.cs
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

namespace Drive.Rdf
{
	/// <summary>
	/// Reprsents an RDF Graph comprising Nodes and Literals connected by Edges
	/// </summary>
	public interface IRdfGraph
	{
		/// <summary>
		/// When implemented by a class, gets the namespaces associated with this RDF Graph
		/// </summary>
		IRdfNamespaceCollection NameSpaces
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, gets the collection of Nodes in this Graph
		/// </summary>
		IRdfNodeCollection Nodes
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, gets the collection of Literals in this Graph
		/// </summary>
		IRdfNodeCollection Literals
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, gets the number of nodes in this RDF Graph
		/// </summary>
		long Count
		{
			get;
		}

		/// <summary>
		/// When implementsd by a class, gets a collection of edges in this RdfGraph
		/// </summary>
		IRdfEdgeCollection Edges
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, adds an edge to the RdfGraph
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		void AddEdge(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, adds a new node to the RdfGraph
		/// </summary>
		/// <param name="nodeUri">A string representing the Uri of the new node</param>
		/// <returns>The newly added node</returns>
		IRdfNode AddNode(string nodeUri);

		/// <summary>
		/// When implemented by a class, adds a new node to the RdfGraph 
		/// </summary>
		/// <param name="node">The IRdfNode to add</param>
		void AddNode(IRdfNode node);

		/// <summary>
		/// When implemented by a class, adds a new literal to the RdfGraph
		/// </summary>
		/// <param name="literalValue">A string representing the value of the new literal</param>
		/// <returns>The newly added IRdfLiteral</returns>
		IRdfLiteral AddLiteral(string literalValue);

		/// <summary>
		/// When implemented by a class, adds a new literal to the RdfGraph
		/// </summary>
		/// <param name="datatypeUri">A string representing the URI that specifies the datatype of the new literal</param>
		/// <param name="langID">A string representing the Language ID of the new Literal</param>
		/// <param name="literalValue">A string representing the value of the new Literal</param>
		/// <returns>The newly added IRdfLiteral</returns>
		IRdfLiteral AddLiteral(string literalValue, string langID, string datatypeUri);

		/// <summary>
		/// When implemented by a class, adds a new literal to the RdfGraph
		/// </summary>
		/// <param name="literal">The IRdfLiteral to add</param>
		void AddLiteral(IRdfLiteral literal);

		/// <summary>
		/// When implemented by a class, gets the node with the specified ID from this graph
		/// </summary>
		IRdfNode this[string nodeID]
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, gets a collection of NTriples.
		/// </summary>
		IRdfNTripleCollection GetNTriples();
		
	}
}
