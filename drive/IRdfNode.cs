/*****************************************************************************
 * IRdfNode.cs
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
	/// Represents a Node in the RDF Graph
	/// </summary>
	public interface IRdfNode
	{
		/// <summary>
		/// When implemented by a class, gets the Collection of child edges associated with this node
		/// </summary>
		IRdfEdgeCollection ChildEdges
		{
			get;
		}
		
		/// <summary>
		/// When implemented by a class, gets the collection of parent edges associated with this node
		/// </summary>
		IRdfEdgeCollection ParentEdges
		{
			get;
		}

		/// <summary>
		/// When implemented by a class, gets or sets the ID of this IRdfNode
		/// </summary>
		string ID
		{
			get;
			set;
		}

		/// <summary>
		/// When implemented by a class, gets or sets the Language ID of this node
		/// </summary>
		string LangID
		{
			get;
			set;
		}

		/// <summary>
		/// When implemented by a class, attaches a child edge to this IRdfNode
		/// </summary>
		/// <param name="edge">The edge to attach</param>
		void AttachChildEdge(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, attaches a parent edge to this node
		/// </summary>
		/// <param name="edge">The edge to attach</param>
		void AttachParentEdge(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, detaches a child edge from this node
		/// </summary>
		/// <param name="edge">The edge to detach</param>
		void DetachChildEdge(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, detaches a parent edge from this node
		/// </summary>
		/// <param name="edge">The edge to detach</param>
		void DetachParentEdge(IRdfEdge edge);

		/// <summary>
		/// When implemented by a class, returns the N-Triple representation of this node
		/// </summary>
		/// <returns>A string containing the N-Triple representation of this node</returns>
		string ToNTriple();
	}
}
