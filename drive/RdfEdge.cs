/*****************************************************************************
 * RdfEdge.cs
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
using System.Text;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents an Edge in the RDF Graph
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfEdge : IRdfEdge
	{
		/// <summary>
		/// The parent node of this edge
		/// </summary>
		private RdfNode _parentNode;
		/// <summary>
		/// Gets or sets the parent node of this edge
		/// </summary>
		public IRdfNode ParentNode
		{
			get
			{
				return _parentNode;
			}
			set
			{
				_parentNode = (RdfNode)value;
			}
		}

		/// <summary>
		/// The child node of this edge
		/// </summary>
		private RdfNode _childNode;

		/// <summary>
		/// Gets or sets the child node of this edge
		/// </summary>
		public IRdfNode ChildNode
		{
			get
			{
				return _childNode;
			}
			set
			{
				_childNode = (RdfNode)value;
			}
		}

		/// <summary>
		/// The URI of this edge
		/// </summary>
		private string _edgeID;

		/// <summary>
		/// Gets or sets the URI of this edge
		/// </summary>
		/// <exception cref="UriFormatException">The specified value is a null reference</exception>
		public string ID
		{
			get
			{
				return _edgeID;
			}
			set
			{
				Uri u = new Uri(value);
				_edgeID = value;
			}
		}

		/// <summary>
		/// The language ID of this edge
		/// </summary>
		private string _langID;

		/// <summary>
		/// Gets or sets the Language ID of this edge
		/// </summary>
		/// <remarks>This language ID is inherited by all child nodes and edges unless overridden</remarks>
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
		/// Attaches a Child node to this edge
		/// </summary>
		/// <param name="node">The node to attach</param>
		/// <exception cref="ArgumentNullException">The specified node is a null reference</exception>
		public void AttachChildNode(IRdfNode node)
		{
			if(node == null)
				throw (new ArgumentNullException());
			DetachChildNode();
			ChildNode = node;
			node.ParentEdges.Add(this);
		}

		/// <summary>
		/// Detaches the child node
		/// </summary>
		/// <returns>The newly detached child node. Returns null if no child node was present</returns>
		public IRdfNode DetachChildNode()
		{
			IRdfNode node = ChildNode;
			if(ChildNode != null)
				ChildNode.DetachParentEdge(this);
			return node;
		}

		/// <summary>
		/// Attaches a parent node to this edge
		/// </summary>
		/// <param name="node">The node to attach</param>
		/// <exception cref="ArgumentNullException">The specified nodenode is a null reference</exception>
		public void AttachParentNode(IRdfNode node)
		{
			if(node == null)
				throw (new ArgumentNullException());
			DetachParentNode();
			ParentNode = node;
			node.ChildEdges.Add(this);
		}

		/// <summary>
		/// Detaches the parent node from this edge
		/// </summary>
		/// <returns>The newly detached parent node. Returns null if no parent node was present</returns>
		public IRdfNode DetachParentNode()
		{
			IRdfNode node = ParentNode;
			if(ParentNode != null)
				ParentNode.DetachChildEdge(this);
			return node;
		}

		/// <summary>
		/// Initializes a new instance of the RdfEdge class. Sets the ID, ChildNode and ParentNode properties to null
		/// </summary>
		public RdfEdge()
		{
			_edgeID = "";
			_childNode = null;
			_parentNode = null;
		}

		/// <summary>
		/// Initializes a new instance of the RdfEdge class with the given URI
		/// </summary>
		/// <param name="edgeUri">A string representing the Uri of this edge. The ChildNode and ParentNode properties are set to null</param>
		/// <exception cref="UriFormatException">The specified edgeUri was not a well formed URI</exception>
		public RdfEdge(string edgeUri)
		{
			 Uri u = new Uri(edgeUri);
			_edgeID = edgeUri;
			_childNode = null;
			_parentNode = null;
		}

		/// <summary>
		/// Converts this Edge to its NTriple representation
		/// </summary>
		/// <returns>A string with the N-Triple representing of this edge</returns>
		public virtual string ToNTriple()
		{
			return Util.ToCharmod("<"+ID+">");
		}
	}
}
