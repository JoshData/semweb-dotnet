/*****************************************************************************
 * IRdfedgeList.cs
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
	/// Represents a collection of IRdfEdge objects
	/// </summary>
	public interface IRdfEdgeList
	{
		/// <summary>
		/// When implemented by a class, gets the total number of members in this collection
		/// </summary>
		int Count
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, gets the IRdfEdge at the specified index
		/// </summary>
		IRdfEdge this[int index]
		{
			get;
			set;
		}

		/// <summary>
		/// When implemented by a class, returns an enumerator that can iterate through the collection
		/// </summary>
		IEnumerator GetEnumerator();

		/// <summary>
		/// When implemented by a class, adds an IRdfEdge object to the collection
		/// </summary>
		/// <param name="edge">The IRdfEdge to add to the collection</param>
		void Add(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, determines whether the specified IRdfEdge is a member of this collection
		/// </summary>
		/// <param name="edge">An IRdfEdge</param>
		/// <returns>True if the specified edge belongs to the collection.</returns>
		bool Contains(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, removes the specified IRdfEdge object from the collection
		/// </summary>
		/// <param name="edge">The edge to remove</param>
		void Remove(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class removes all the edges from this collection
		/// </summary>
		void RemoveAll();
	}
}
