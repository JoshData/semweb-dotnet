/*****************************************************************************
 * RdfNodeCollection.cs
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
using System.Collections;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents a collection of RDF Nodes
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfNodeCollection : IRdfNodeCollection
	{
		/// <summary>
		/// The collection of RdfNode objects
		/// </summary>
		private Hashtable _nodes;

		/// <summary>
		/// Gets or sets the Node with the specified ID
		/// </summary>
		/// <exception cref="ArgumentNullException">nodeID is a null reference.</exception>
		public IRdfNode this[string nodeID]
		{
			get
			{
				return (RdfNode)_nodes[nodeID];
			}
			set
			{
				_nodes[nodeID] = value;
			}
		}

		/// <summary>
		/// Gets an enumerator that can iterate through this collection.
		/// </summary>
		/// <returns>An object that implements that implements the <see cref="IEnumerator"/> interface.</returns>
		public IEnumerator GetEnumerator()
		{
			return _nodes.GetEnumerator();
		}

		/// <summary>
		/// Gets the total number of nodes in this collection.
		/// </summary>
		public int Count
		{
			get
			{
				return _nodes.Count;
			}
		}

		/// <summary>
		/// Adds a node to the collection.
		/// </summary>
		/// <param name="nodeID">The ID of the node to add.</param>
		/// <param name="newNode">An object that implements the IRdfNode interface. This is a reference to the node to add.</param>
		/// <exception cref="ArgumentException">A node with the specified ID already exists in the collection.</exception>
		/// <exception cref="ArgumentNullException">The specified ID is a null reference.</exception>
		public void Add(string nodeID, IRdfNode newNode)
		{
			if(newNode == null)
				throw (new ArgumentNullException());
			_nodes.Add(nodeID,newNode);
		}

		/// <summary>
		/// Adds a node to the collection.
		/// </summary>
		/// <param name="newNode">An object that implements the IRdfNode interface. This is a reference to the node to add.</param>
		/// <exception cref="ArgumentException">A node with the same ID already exists in the collection.</exception>
		/// <exception cref="ArgumentNullException">The ID of the specified node is a null Reference.</exception>
		public void Add(IRdfNode newNode)
		{
			Add(newNode.ID,newNode);
		}

		/// <summary>
		/// Removes a node from this collection.
		/// </summary>
		/// <param name="node">An object that implements the IRdfNode interface. This is the node to remove.</param>
		/// <returns>True if a node with the same ID was found and removed.</returns>
		/// <exception cref="ArgumentException">node is a null reference.</exception>
		/// <remarks>This method removes the node with the same ID as the specified node.</remarks>
		public bool Remove(IRdfNode node)
		{
			if(node == null)
				throw (new ArgumentNullException());
			if(Contains(node))
			{
				_nodes.Remove(node.ID);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Removes all the nodes from this collection
		/// </summary>
		public void RemoveAll()
		{
			_nodes.Clear();
		}

		/// <summary>
		/// Determines whether the specified node is a member of this collection.
		/// </summary>
		/// <param name="node">An object that implements the IRdfNode interface.</param>
		/// <returns>True if a node with the same ID was found in the collection.</returns>
		public bool Contains(IRdfNode node)
		{
			if(node == null)
				return false;
			RdfNode rNode = (RdfNode)_nodes[node.ID];
			if(rNode != null)
				return (rNode == node);
			return false;
		}

		/// <summary>
		/// Initializes a new instance of the RdfNodeCollection class.
		/// </summary>
		public RdfNodeCollection()
		{
			_nodes = new Hashtable();
		}
	}
}
