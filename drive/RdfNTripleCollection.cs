/*****************************************************************************
 * RdfNTripleCollection.cs
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
	/// Summary description for RdfN3Collection.
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfNTripleCollection : IRdfNTripleCollection
	{
		/// <summary>
		/// A list of triples in this collection
		/// </summary>
		private ArrayList _triples;

		/// <summary>
		/// Gets a count of the total number of triples in this collection
		/// </summary>
		public int Count
		{
			get
			{
				return _triples.Count;
			}
		}

		/// <summary>
		/// Gets or sets the triple at the specified index
		/// </summary>
		public IRdfNTriple this[int index]
		{
			get
			{
				return (RdfNTriple)_triples[index];
			}
			set
			{
				_triples[index] = value;
			}
		}


		/// <summary>
		/// Adds a triple to this collection
		/// </summary>
		/// <param name="triple">The triple to add. This is an object that implements the IRdfNTriple interface</param>
		/// <exception cref="ArgumentNullException">The specified triple is a null reference.</exception>
		public void Add(IRdfNTriple triple)
		{
			if(triple == null)
				throw (new ArgumentNullException());
			_triples.Add(triple);
		}

		/// <summary>
		/// Determines whether this collection contains the specified object
		/// </summary>
		/// <param name="triple">An object that implements the IRdfNTriple interface</param>
		/// <returns>True if the specified object is present in this collection</returns>
		/// <remarks>This method calls ArrayList.Contains to determine whether the triple exists and does not 
		/// check for the presence of triples with the same contents as the specified triple.</remarks>
		public bool Contains(IRdfNTriple triple)
		{
			if(triple == null)
				return false;
			return _triples.Contains(triple);
		}

		/// <summary>
		/// Removes the specified N-Triple object from the collection if it exists
		/// </summary>
		/// <param name="triple">An object that implements the IRdfNTriple interface</param>
		/// <remarks>This method calls ArrayList.Remove to remove the triple if it exists and does not 
		/// remove triples with the same contents as the specified triple.</remarks>
		public void Remove(IRdfNTriple triple)
		{
			_triples.Remove(triple);
		}

		/// <summary>
		/// Removes all the N-Triples from this collection
		/// </summary>
		public void RemoveAll()
		{
			_triples.Clear();
		}

		/// <summary>
		/// Gets an enumerator that can iterate through this collection
		/// </summary>
		/// <returns>An object that implements the IEnumerator interface</returns>
		public IEnumerator GetEnumerator()
		{
			return _triples.GetEnumerator();
		}

		/// <summary>
		/// Initializes a new instance of the RdfNTripleCollection class.
		/// </summary>
		public RdfNTripleCollection()
		{
			_triples = new ArrayList();
		}
	}
}
