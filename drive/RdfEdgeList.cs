/*****************************************************************************
 * RdfEdgeList.cs
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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents a collection of RdfEdge objects
	/// </summary>
	/// <remarks>This class implements the IRdfEdgeCollection interface.
	/// This collection has been implemented using an Arraylist and this enables the collection to have duplicate entries.
	/// This is required because a particular node in the RDF graph can have multiple child (or parent) edges that point to distinct nodes.</remarks>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfEdgeList : IRdfEdgeList
	{
		/// <summary>
		/// An arraylist of edges
		/// </summary>
		protected ArrayList _edges;

		/// <summary>
		/// Gets or sets the edge at the specified index
		/// </summary>
		public virtual IRdfEdge this[int index]
		{
			get
			{
				return (RdfEdge)_edges[index];
			}
			set
			{
				_edges[index] = value;
			}
		}

		/// <summary>
		/// Gets the number of edges stored in this collection
		/// </summary>
		public virtual int Count
		{
			get
			{
				return _edges.Count;
			}
		}

		/// <summary>
		/// Adds an RdfEdge to this collection
		/// </summary>
		/// <param name="newEdge">The Edge to add.</param>
		/// <exception cref="ArgumentNullException">newEdge is a null reference.</exception>
		public virtual void Add(IRdfEdge newEdge)
		{
			if(newEdge == null)
				throw (new ArgumentNullException());
			_edges.Add(newEdge);
		}

		/// <summary>
		///	Determines whether an edge is present in this collection
		/// </summary>
		/// <param name="edge">The Edge to locate in the collection</param>
		/// <returns>True if the Edge was found in the collection</returns>
		/// <remarks>This method is a wrapper around the ArrayList.Contains(object) method and thus is O(<i>n</i>) operation.
		/// Additionally ArrayList.Contains calls Object.equals to determine equality and so you cannot use this method to determine if an edge with the same edgeID is a member of this collection.</remarks>
		public virtual bool Contains(IRdfEdge edge)
		{
			if(edge == null)
				return false;
			return _edges.Contains(edge);
		}

		/// <summary>
		/// Removes an edge object from the collection
		/// </summary>
		/// <param name="edge">The edge to remove</param>
		/// <returns>True if the edge was successfully removed</returns>
		///	<remarks>If the edge exists then it is removed by calling the ArrayList.Remove method which is an O(n) operation.</remarks>
		public virtual void Remove(IRdfEdge edge)
		{
			if(edge == null)
				throw (new ArgumentNullException());
			_edges.Remove(edge);
		}

		/// <summary>
		/// Removes all the edges from this collection
		/// </summary>
		public virtual void RemoveAll()
		{
			_edges.Clear();
		}

		internal ArrayList EdgeList
		{
			get
			{
				return _edges;
			}
		}
		/// <summary>
		/// Gets an enumerator that can iterate through this collection
		/// </summary>
		/// <returns>An object that implements the IEnumerator interface</returns>
		public virtual IEnumerator GetEnumerator()
		{
			return _edges.GetEnumerator();
		}

		/// <summary>
		/// Initializes a new instance of this collection
		/// </summary>
		public RdfEdgeList()
		{
			_edges = new ArrayList();
		}

		/// <summary>
		/// Initializes a new instance of this collection from an existing one
		/// </summary>
		/// <param name="edges"></param>
		public RdfEdgeList(IRdfEdgeList edges)
		{
			Debug.Assert(edges != null);
			_edges = ((RdfEdgeList)edges).EdgeList;
		}
	}
}
