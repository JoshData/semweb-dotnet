/*****************************************************************************
 * IRdfEdgeCollection.cs
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

namespace Drive.Rdf
{
	/// <summary>
	/// Represents a collection of objects that implement the IRdfEdge interface. 
	/// This collection maps edge IDs to objects that implement the IRdfEdgeList interface
	/// </summary>
	public interface IRdfEdgeCollection
	{
		/// <summary>
		/// When implemented by a class, returns the IRdfEdge at the specified index
		/// </summary>
		IRdfEdge this[int index]
		{
			get;
		}
		/// <summary>
		/// When implemented by a class, returns a list of edges in this collection with the specified ID
		/// </summary>
		IRdfEdgeList this[string edgeID]
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, returns the IRdfEdge at the given index from the list of edges with the specified ID
		/// </summary>
		IRdfEdge this[string edgeID, int index]
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, adds the specified edge to this collection
		/// </summary>
		/// <param name="edgeID">The ID of the edge</param>
		/// <param name="edge">The IRdfEdge object</param>
		void Add(string edgeID, IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, adds the specified edge to this collection
		/// </summary>
		/// <param name="edge">The IRdfEdge object</param>
		void Add(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, returns the total number of edges in this collection
		/// </summary>
		int Count
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, determines whether the collection contains any edge with the specified ID
		/// </summary>
		/// <param name="edgeID">A string containing an ID</param>
		/// <returns>True if this collection contains any edge with the specified ID</returns>
		bool Contains(string edgeID);

		/// <summary>
		/// When implemented by a class, determines whether the collection contains the specified IRdfEdge object
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		/// <returns>True if this collection contains the specified object</returns>
		bool Contains(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, removes the specified IRdfEdge object
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface</param>
		void Remove(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class removes all the edges from this collection
		/// </summary>
		void RemoveAll();

		/// <summary>
		/// When implemented by a class, gets an enumerator that can iterate through the collection
		/// </summary>
		IEnumerator GetEnumerator();
	}
}
