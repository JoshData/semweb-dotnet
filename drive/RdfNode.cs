/*****************************************************************************
 * RdfNode.cs
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
using System.Text;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents a node in the RDF Graph
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfNode : IRdfNode
	{
		/// <summary>
		/// The URI of the node.
		/// </summary>
		private string _nodeID;
		
		/// <summary>
		/// Gets or sets the ID of this node
		/// </summary>
		/// <exception cref="UriFormatException">Attempt to set the ID to a value that is not a well formed URI.</exception>
		public virtual string ID
		{
			get
			{
				return _nodeID;
			}
			set
			{
				 Uri u = new Uri(value);
				_nodeID = value;
			}
		}

		/// <summary>
		/// The collection of parent edges associated with this node
		/// </summary>
		private RdfEdgeCollection _parentEdges;
		
		/// <summary>
		/// Gets the parent edges of this Node.
		/// </summary>
		public IRdfEdgeCollection ParentEdges
		{
			get
			{
				return _parentEdges;
			}
		}

		/// <summary>
		/// The collection of child edges associated with this node
		/// </summary>
		private RdfEdgeCollection _childEdges;
		
		/// <summary>
		/// Gets the Child Edges of this node.
		/// </summary>
		public IRdfEdgeCollection ChildEdges
		{
			get
			{
				return _childEdges;
			}
		}

		/// <summary>
		/// The language identifier for this node.
		/// </summary>
		private string _langID;

		/// <summary>
		/// Gets or stes the language identifier for this node.
		/// </summary>
		public string LangID
		{
			get
			{
				return _langID;
			}
			set
			{
				_langID = value;
			}
		}

		/// <summary>
		/// Attaches a child edge to this node.
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface. This is the new edge to attach.</param>
		/// <exception cref="ArgumentNullException">The specified edge is a null reference.</exception>
		public void AttachChildEdge(IRdfEdge edge)
		{
			if(edge == null)
				throw (new ArgumentNullException());
			ChildEdges.Add(edge);
			edge.ParentNode = this;
		}

		/// <summary>
		/// Detaches a child edge from this node.
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface.</param>
		/// <exception cref="ArgumentNullException">The specified edge is a null reference.</exception>
		public void DetachChildEdge(IRdfEdge edge)
		{
			if(edge == null)
				throw (new ArgumentNullException());
			if(ChildEdges.Contains(edge))
			{
				ChildEdges.Remove(edge);
				edge.ParentNode = null;
			}
		}

		/// <summary>
		/// Attaches a parent edge to this node.
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface.</param>
		/// <exception cref="ArgumentNullException">The specified edge is a null reference.</exception>
		public void AttachParentEdge(IRdfEdge edge)
		{
			if(edge == null)
				throw (new ArgumentNullException());
			ParentEdges.Add(edge);
			edge.ChildNode = this;
		}

		/// <summary>
		/// Detaches a parent edge from this node.
		/// </summary>
		/// <param name="edge">An object that implements the IRdfNode interface.</param>
		/// <exception cref="ArgumentNullException">The specified edge is a null reference.</exception>
		public void DetachParentEdge(IRdfEdge edge)
		{
			if(edge == null)
				throw (new ArgumentNullException());
			if(ParentEdges.Contains(edge))
			{
				ParentEdges.Remove(edge);
				edge.ChildNode = null;
			}
		}
		/// <summary>
		/// Initializes a new instance of the RdfNode class.
		/// </summary>
		public RdfNode()
		{            
			_parentEdges = new RdfEdgeCollection();
			_childEdges = new RdfEdgeCollection();
			_nodeID = null;
			LangID = "";
		}

		/// <summary>
		/// Initializes a new instance of the RdfNode class with the specified URI.
		/// </summary>
		/// <param name="nodeUri">A string representing the URI of this node.</param>
		/// <exception cref="UriFormatException">The specified URI is a not a well formed URI.</exception>
		public RdfNode(string nodeUri)
		{            
			_parentEdges = new RdfEdgeCollection();
			_childEdges = new RdfEdgeCollection();
			//if the nodeUri is not a well formed Uri then throw a UriFormatException
			Uri u = new Uri(nodeUri);
			_nodeID = nodeUri;
			LangID = "";
		}

		/// <summary>
		/// Returns an N-Triple representation of this Node
		/// </summary>
		/// <returns>A string containing the N-Triple representation of this Node.</returns>
		public virtual string ToNTriple()
		{
			if(ID.ToLower().StartsWith("blankid:"))
				return "_:"+ID.Substring(8);

			return Util.ToCharmod("<"+ID+">");
		}

		/// <summary>
		/// Returns an string representation of this Node
		/// </summary>
		/// <returns>A string containing the string representation of this Node.</returns>
		public override string ToString()
		{
			return ID;
		}
	}
}
