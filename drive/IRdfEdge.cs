/*****************************************************************************
 * IRdfEdge.cs
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

namespace Drive.Rdf
{
	/// <summary>
	/// Defines a generalized mechanism for processing edges in the RDF Graph
	/// </summary>
	public interface IRdfEdge
	{
		/// <summary>
		/// When implemented by a class, attaches a Child Node to this IRdfEdge
		/// </summary>
		/// <param name="node">The IRdfNode to attach</param>
		void AttachChildNode(IRdfNode node);
		/// <summary>
		/// When implemented by a class, attaches a Parent Node to this IRdfEdge
		/// </summary>
		/// <param name="node">The IRdfNode to attach</param>
		void AttachParentNode(IRdfNode node);

		/// <summary>
		/// When implemented by a class, detachs the Child Node of this IRdfEdge
		/// </summary>
		/// <returns>The removed IRdfNode</returns>
		IRdfNode DetachChildNode();

		/// <summary>
		/// When implemented by a class, detaches the Parent Node of this IRdfEdge
		/// </summary>
		/// <returns>The removed IRdfNode</returns>
		IRdfNode DetachParentNode();

		/// <summary>
		/// When implemented by a class, returns the N-triple representation of this IRdfEdge
		/// </summary>
		/// <returns>A string containing the N-Triple representation of this IRdfEdge</returns>
		string ToNTriple();

		/// <summary>
		/// When implemented by a class, gets or sets the Child Node of this IRdfEdge
		/// </summary>
		IRdfNode ChildNode
		{
			get;
			set;
		}
		/// <summary>
		/// When implemented by a class, gets or sets the Parent Node of this IRdfEdge
		/// </summary>
		IRdfNode ParentNode
		{
			get;
			set;
		}
		/// <summary>
		/// When implemented by a class, gets or sets the ID of this IRdfEdge
		/// </summary>
		string ID
		{
			get;
			set;
		}

		/// <summary>
		/// When implemented by a class, gets or sets the Language ID of this IRdfEdge
		/// </summary>
		string LangID
		{
			get;
			set;
		}
	}
}
