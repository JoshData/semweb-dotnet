/*****************************************************************************
 * IRdfNodeCollection.cs
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
	/// Represents a collection of IRdfNode objects
	/// </summary>
	public interface IRdfNodeCollection
	{
		/// <summary>
		/// When implemented by a class, gets the total number of IRdfNodes in this collection
		/// </summary>
		int Count
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, gets or sets the node with the given ID
		/// </summary>
		IRdfNode this[string nodeID]
		{
			get;
			set;
		}

		/// <summary>
		/// When implemented by a class, gets an enumerator that can iterate through the collection
		/// </summary>
		IEnumerator GetEnumerator();

		/// <summary>
		/// When implemented by a class, adds a new node to the collection
		/// </summary>
		/// <param name="nodeID">A string containing the ID of the new node</param>
		/// <param name="newNode">The new node to add</param>
		void Add(string nodeID, IRdfNode newNode);

		/// <summary>
		/// When implemented by a class, adds a new node to the collection
		/// </summary>
		/// <param name="newNode">The new node to add</param>
		void Add(IRdfNode newNode);

		/// <summary>
		/// When implemented by a class, removes a node from the collection
		/// </summary>
		/// <param name="node"></param>
		/// <returns>True if the node was found and removed</returns>
		bool Remove(IRdfNode node);

		/// <summary>
		/// When implemented by a class removes all the nodes from this collection
		/// </summary>
		void RemoveAll();

		/// <summary>
		/// When implemented by a class, determines whether the specified node exists in the collection
		/// </summary>
		/// <param name="node">An IRdfNode</param>
		/// <returns>True if the specified node was found in the collection</returns>
		bool Contains(IRdfNode node);
	}
}
