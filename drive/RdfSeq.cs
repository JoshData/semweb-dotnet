/*****************************************************************************
 * RdfSeq.cs
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
	/// Represents an RDF container of type rdf:Seq.
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfSeq : RdfContainer, IRdfSeq
	{
		/// <summary>
		/// Initializes a new instance of the RdfSeq class
		/// </summary>
		/// <remarks>This constructor creates a new RdfNode with Uri rdf:Seq and sets it as the child node of an edge with URI rdf:type</remarks>	
		public RdfSeq()
		{
			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_typeEdge.AttachChildNode(new RdfNode(RdfNamespaceCollection.RdfNamespace+"Seq"));
			AttachChildEdge(_typeEdge);
		}

		/// <summary>
		/// Initializes a new instance of the RdfSeq class with the given Uri and TypeNode
		/// </summary>
		/// <param name="nodeUri">A string representing the Uri of this Container</param>
		/// <param name="typeNode">The RdfNode object to attach to the edge specifying the type. This is usually a node with ID rdf:Seq.</param>
		/// <exception cref="ArgumentNullException">typeNode is a null reference</exception>
		public RdfSeq(string nodeUri, RdfNode typeNode)
		{
			if(typeNode == null)
				throw(new ArgumentNullException());
			ID = nodeUri;
			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_typeEdge.AttachChildNode(typeNode);
			AttachChildEdge(_typeEdge);
		}
		/// <summary>
		/// Initializes a new instance of the RdfSeq class with the given Uri
		/// </summary>
		/// <param name="nodeUri">A string representing the URI of this Container</param>
		/// <remarks>This constructor creates a new RdfNode with URI rdf:Seq and sets it as the child node of an edge with URI rdf:type</remarks>	
		public RdfSeq(string nodeUri)
		{
			ID = nodeUri;
			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_typeEdge.AttachChildNode(new RdfNode(RdfNamespaceCollection.RdfNamespace+"Seq"));
			AttachChildEdge(_typeEdge);
		}
	}
}
