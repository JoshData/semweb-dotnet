/*****************************************************************************
 * RdfStatement.cs
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
	/// Represents a reified statement
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfStatement : RdfNode, IRdfStatement
	{
		/// <summary>
		/// The edge that specifies the type of this node. 
		/// </summary>
		private RdfEdge _typeEdge;

		/// <summary>
		/// Gets or sets the Node that specifies the type of this node
		/// </summary>
		/// <exception cref="ArgumentNullException">Attempt to set this property to a null reference.</exception>
		public IRdfEdge Type
		{
			get
			{
				return _typeEdge;
			}
		}

		/// <summary>
		/// The edge that points to the Subject of this statement.
		/// </summary>
		private RdfEdge _rdfSubjectEdge;

		/// <summary>
		/// Gets or sets the Subject of this statement.
		/// </summary>
		/// <exception cref="ArgumentNullException">Attempt to set this property to a null reference.</exception>
		public IRdfEdge RdfSubject
		{
			get
			{
                return _rdfSubjectEdge;
			}
		}

		/// <summary>
		/// The edge that points to the Predicate of this statement.
		/// </summary>
		private RdfEdge _rdfPredicateEdge;

		/// <summary>
		/// Gets or sets the Predicate of this statement.
		/// </summary>
		/// <exception cref="ArgumentNullException">Attempt to set this property to a null reference.</exception>
		public IRdfEdge RdfPredicate
		{
			get
			{
				return _rdfPredicateEdge;
			}
		}

		/// <summary>
		/// The edge that points to the object of this statement.
		/// </summary>
		private RdfEdge _rdfObjectEdge;

		/// <summary>
		/// Gets or sets the Object of this statement
		/// </summary>
		/// <exception cref="ArgumentNullException">Attempt to set this property to a null reference.</exception>
		public IRdfEdge RdfObject
		{
			get
			{
				return _rdfObjectEdge;
			}
		}

		/// <summary>
		/// Initializes a new instance of the RdfStatement class.
		/// </summary>
		public RdfStatement()
		{
			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_rdfSubjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"subject");
			_rdfPredicateEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"predicate");
			_rdfObjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"object");

			_typeEdge.AttachChildNode(new RdfNode(RdfNamespaceCollection.RdfNamespace+"Statement"));

			AttachChildEdge(_typeEdge);
			AttachChildEdge(_rdfSubjectEdge);
			AttachChildEdge(_rdfPredicateEdge);
			AttachChildEdge(_rdfObjectEdge);
		}

		/// <summary>
		/// Initializes a new instance of the RdfStatement class with the specified URI.
		/// </summary>
		/// <param name="nodeUri">A string that contains the URI of this statement.</param>
		/// <exception cref="UriFormatException">nodeUri is not a well formed URI.</exception>
		public RdfStatement(String nodeUri)
		{
			ID = nodeUri;
			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			
			_rdfSubjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"subject");
			_rdfPredicateEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"predicate");
			_rdfObjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"object");
			
			_typeEdge.AttachChildNode(new RdfNode(RdfNamespaceCollection.RdfNamespace+"Statement"));
			
			AttachChildEdge(_typeEdge);
			AttachChildEdge(_rdfSubjectEdge);
			AttachChildEdge(_rdfPredicateEdge);
			AttachChildEdge(_rdfObjectEdge);
		}

		/// <summary>
		/// Initializes a new instance of the RdfStatement class with the node that specifies the Type.
		/// </summary>
		/// <param name="type">The node that specifies the type of this node.</param>
		/// <exception cref="ArgumentNullException">type is a null reference.</exception>
		public RdfStatement(RdfNode type)
		{
			if(type == null)
				throw(new ArgumentNullException());

			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			
			_rdfSubjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"subject");
			_rdfPredicateEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"predicate");
			_rdfObjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"object");
	
			_typeEdge.AttachChildNode(type);
			
			AttachChildEdge(_typeEdge);
			AttachChildEdge(_rdfSubjectEdge);
			AttachChildEdge(_rdfPredicateEdge);
			AttachChildEdge(_rdfObjectEdge);
		}

		/// <summary>
		/// Initializes a new instance of the RdfStatement class.
		/// </summary>
		/// <param name="nodeUri">A string that contains the URI of this statement.</param>
		/// <param name="type">The node that specifies the type of this node.</param>
		/// <exception cref="ArgumentNullException">type is a null reference.</exception>
		/// <exception cref="UriFormatException">nodeUri is not a well formed URI.</exception>
		public RdfStatement(String nodeUri, RdfNode type)
		{
			if(type == null)
				throw(new ArgumentNullException());
			
			ID = nodeUri;

			_typeEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"type");
			_rdfSubjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"subject");
			_rdfPredicateEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"predicate");
			_rdfObjectEdge = new RdfEdge(RdfNamespaceCollection.RdfNamespace+"object");

			_typeEdge.AttachChildNode(type);

			AttachChildEdge(_typeEdge);
			AttachChildEdge(_rdfSubjectEdge);
			AttachChildEdge(_rdfPredicateEdge);
			AttachChildEdge(_rdfObjectEdge);
		}
	}
}
