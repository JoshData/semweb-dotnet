/*****************************************************************************
 * RdfNamespaceCollection.cs
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
	/// Represents a collection of Namespaces
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfNamespaceCollection : IRdfNamespaceCollection
	{
		/// <summary>
		/// The RDF Namespace.
		/// </summary>
		public const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

		/// <summary>
		/// The XML namespace.
		/// </summary>
		public const string XmlNamespace = "http://www.w3.org/XML/1998/namespace#";
		
		/// <summary>
		/// The collection of namespaces
		/// </summary>
		private Hashtable _nameSpaces;

		/// <summary>
		/// Gets or sets the Namespace with the specified name
		/// </summary>
		/// <exception cref="ArgumentException">Attempt to set a namespace with an empty name.</exception>
		/// <exception cref="ArgumentNullException">Attempt to store a null namespace.</exception>
		/// <exception cref="ArgumentNullException">Attempt to set a namespace with a null name.</exception>
		/// <remarks>The name of the namspace is a string consisting of the namespace prefix prefaced with xmlns:. 
		/// The one exception is xml:base where the value of the base URI of the RDF document is stored under the
		/// name xml:base.</remarks>
		public string this[string name]
		{
			get
			{
				return (String)_nameSpaces[name];
			}
			set
			{
				if(name == null)
					throw(new ArgumentNullException("The namespace name cannot be null."));
				if(name.Length == 0)
					throw(new ArgumentException("The namespace name cannot be an empty string."));
				if(value == null)
					throw(new ArgumentNullException("The namespace value cannot be null"));
				_nameSpaces[name] = value;
			}
		}

		/// <summary>
		/// Removes a namespace from the namespace collection
		/// </summary>
		/// <param name="name">The name of the namespace to remove</param>
		/// <exception cref="ArgumentNullException">The specified name is a null reference.</exception>
		/// <remarks>Removes the name from the collection.</remarks>
		public void Remove(string name)
		{
			_nameSpaces.Remove(name);
		}

		/// <summary>
		/// Removes all the namespaces from this collection
		/// </summary>
		public void RemoveAll()
		{
			_nameSpaces.Clear();
		}
		/// <summary>
		/// Gets the total number of namespaces in this collection.
		/// </summary>
		public int Count
		{
			get
			{
				return _nameSpaces.Count;
			}
		}

		/// <summary>
		/// Gets an enumerator that can iterate through this collection.
		/// </summary>
		/// <returns>An object that implements that implements the <see cref="IEnumerator"/> interface.</returns>
		public IEnumerator GetEnumerator()
		{
			return _nameSpaces.GetEnumerator();
		}

		/// <summary>
		/// Initializes a new instance of the RdfNamespaceCollection class.
		/// </summary>
		public RdfNamespaceCollection()
		{
			_nameSpaces = new Hashtable();
		}
	}
}
