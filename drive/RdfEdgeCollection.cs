/*****************************************************************************
 * RdfEdgeCollection.cs
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
	/// Represents a collection of edges. This class maps edge IDs to lists of RdfEdge objects
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfEdgeCollection : IRdfEdgeCollection
	{
		/// <summary>
		/// The hashtable containing the map
		/// </summary>
		private Hashtable _edgeMap;

		/// <summary>
		/// List of all the edges in this collection
		/// </summary>
		private RdfEdgeList _edges;

		/// <summary>
		/// Map of Edge objects to an index into the list of edges
		/// </summary>
		private Hashtable _edgeIndexMap;

		/// <summary>
		/// Initializes a new instance of the RdfEdgeCollection class.
		/// </summary>
		public RdfEdgeCollection()
		{
			_edgeMap = new Hashtable();
			_edgeIndexMap = new Hashtable();
			_edges = new RdfEdgeList();
		}

		/// <summary>
		/// Returns a list of edges with the specified Edge ID
		/// </summary>
		public IRdfEdgeList this[string edgeID]
		{
			get
			{
				RdfEdgeList edgeList = (RdfEdgeList)_edgeMap[edgeID];
				if(edgeList != null)
					return (IRdfEdgeList)new ReadOnlyRdfEdgeList(edgeList);
				else
					return null;
			}
		}

		/// <summary>
		/// Returns the edge at the given index from the list of edges with the specified ID
		/// </summary>
		public IRdfEdge this[string edgeID, int index]
		{
			get
			{
				IRdfEdgeList edges = (IRdfEdgeList)_edgeMap[edgeID];
				if(edges == null)
					return null;
				try
				{
					return edges[index];
				}
				catch(ArgumentOutOfRangeException)
				{
					return null;
				}
			}
		}

		/// <summary>
		/// Returns an edge at the given index
		/// </summary>
		public IRdfEdge this[int index]
		{
			get
			{
				try
				{
					return _edges[index];
				}
				catch(ArgumentOutOfRangeException)
				{
					return null;
				}
			}
		}

		/// <summary>
		/// Returns the total number of edges contained in this collection
		/// </summary>
		public int Count
		{
			get
			{
				return _edges.Count;
			}
		}

		/// <summary>
		/// Adds an edge to this collection
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		/// <exception cref="ArgumentNullException">The specified edge is a null reference</exception>
		public void Add(IRdfEdge edge)
		{
			Add(edge.ID, edge);
		}

		/// <summary>
		/// Adds an edge to this collection
		/// </summary>
		/// <param name="edgeID">The ID of the edge</param>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		/// <exception cref="ArgumentNullException">The specified edge is a null reference or the specified edgeID is a null reference</exception>
		public void Add(string edgeID, IRdfEdge edge)
		{
			IRdfEdgeList edgeList = (IRdfEdgeList)_edgeMap[edgeID];
			if(edgeList == null)
			{
				edgeList = new RdfEdgeList();
				_edgeMap[edgeID] = edgeList;
			}
			edgeList.Add(edge);
			_edges.Add(edge);
		}

		/// <summary>
		/// Removes the specified edge object if it exists.
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		/// <remarks>This method uses object.Equals to determine whether the specified edge exists and then removes it if it is present in the collection</remarks>
		public void Remove(IRdfEdge edge)
		{
			if(edge == null)
				throw(new ArgumentNullException());
			IRdfEdgeList edgeList = (IRdfEdgeList)_edgeMap[edge.ID];
			if(edgeList == null)
				return;
			edgeList.Remove(edge);
			_edges.Remove(edge);
		}

		/// <summary>
		/// Determines whether this collection contains any edges with the specified edge ID
		/// </summary>
		/// <param name="edgeID">A string containing the edge ID</param>
		/// <returns>True if there are any edges in this collection with the specified ID</returns>
		public bool Contains(string edgeID)
		{
			IRdfEdgeList edges = (IRdfEdgeList)_edgeMap[edgeID];
			if((edges == null) || (edges.Count == 0))
				return false;
			return true;
		}
		
		/// <summary>
		/// Removes all the edges from this collection
		/// </summary>
		public void RemoveAll()
		{
			_edges.RemoveAll();
			_edgeMap.Clear();
			_edgeIndexMap.Clear();
		}

		/// <summary>
		/// Determines whether the specified edge object is present in this collection
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		/// <returns>True if this collection contains the specified edge object</returns>
		/// <remarks>This method uses object.Equals to determine whether the specified edge object exists in the collection</remarks>
		public bool Contains(IRdfEdge edge)
		{
			if(!Contains(edge.ID))
				return false;
			return ((IRdfEdgeList)_edgeMap[edge.ID]).Contains(edge);
		}

		/// <summary>
		/// Gets an enumerator that can iterate through the collection
		/// </summary>
		public IEnumerator GetEnumerator()
		{
			return _edges.GetEnumerator();
		}
	}
}
