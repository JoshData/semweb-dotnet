/*****************************************************************************
 * RdfAlt.cs
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
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents an RDF Container of type rdf:Alt
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfAlt : RdfContainer, IRdfAlt
	{
		/// <summary>
		/// Initializes a new instance of the RdfAlt class
		/// </summary>
		/// <remarks>This constructor creates a new RdfNode with URI rdf:Alt and sets it as the child node of an edge with URI rdf:type</remarks>
		public RdfAlt()
		{
			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_typeEdge.AttachChildNode(new RdfNode(RdfNamespaceCollection.RdfNamespace+"Alt"));
			AttachChildEdge(_typeEdge);
		}

		/// <summary>
		/// Initializes a new instance of the RdfAlt class with the specified Uri and the Type
		/// </summary>
		/// <param name="nodeUri">A string specifying a Uri for this container</param>
		/// <param name="typeNode">The RdfNode object to attach the edge specifying the type of container.</param>
		/// <exception cref="ArgumentNullException">typeNode is a null reference</exception>
		/// <remarks>Since this is a container of type rdf:Alt, the specified typeNode  is usually a node with ID rdf:Alt.</remarks>
		public RdfAlt(String nodeUri, RdfNode typeNode)
		{
			if(typeNode == null)
				throw(new ArgumentNullException());
			ID = nodeUri;
			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_typeEdge.AttachChildNode(typeNode);
			AttachChildEdge(_typeEdge);
		}

		/// <summary>
		///	Initializes a new instance of the RdfAlt Class with the given URI
		/// </summary>
		/// <param name="nodeUri">A string representing the Uri of the Container</param>
		/// <remarks>This constructor creates a new RdfNode with Uri rdf:Alt and sets it as the child node of an edge with Uri rdf:type</remarks>
		public RdfAlt(String nodeUri)
		{
			ID = nodeUri;

			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_typeEdge.AttachChildNode(new RdfNode(RdfNamespaceCollection.RdfNamespace+"Alt"));
			AttachChildEdge(_typeEdge);
		}
	}
}
