/*****************************************************************************
 * RdfParser.cs
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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Summary description for RdfParser.
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public abstract class RdfParser
	{
		/// <summary>
		/// The RDF Graph object
		/// </summary>
		protected IRdfGraph _rdfGraph;

		/// <summary>
		/// List of warning messages generated while parsing the RDF/XML
		/// </summary>
		protected ArrayList _warnings;

		/// <summary>
		/// List of warning messages generated while parsing the RDF/XML
		/// </summary>
		public ArrayList Warnings
		{
			get
			{
				return _warnings;
			}
		}

		/// <summary>
		/// List of error messages generated while parsing the RDF/XML
		/// </summary>
		protected ArrayList _errors;

		/// <summary>
		/// List of error messages generated while parsing the RDF/XML
		/// </summary>
		public ArrayList Errors
		{
			get
			{
				return _errors;
			}
		}

		/// <summary>
		/// Indicates whether the parser should throw an exception and stop when it encounters an error
		/// </summary>
		protected bool _stopOnErrors;
		
		/// <summary>
		/// Gets or sets a value that indicates whether the parser should throw an exception and stop when it encounters an error
		/// </summary>
		public bool StopOnErrors
		{
			get
			{
				return _stopOnErrors;
			}
			set
			{
				_stopOnErrors = value;
			}
		}

		/// <summary>
		/// Indicates whether the parser should throw an exception and stop when it generates a warnung
		/// </summary>
		protected bool _stopOnWarnings;

		/// <summary>
		/// Gets or sets a value that indicates whether the parser should throw an exception and stop when it generates a warning
		/// </summary>
		public bool StopOnWarnings
		{
			get
			{
				return _stopOnWarnings;
			}
			set
			{
				_stopOnWarnings = value;
			}
		}

		/// <summary>
		/// Called by the parser when an error is encountered.
		/// </summary>
		/// <param name="msg">The error message associated with the error</param>
		/// <exception cref="InvalidRdfException">ExceptionsOnError is set to true</exception>
		protected void OnError(string msg)
		{
			Errors.Add(msg);
			if(StopOnErrors)
				OnError(new InvalidRdfException(msg));
		}

		/// <summary>
		/// Called by the parser when an error is encountered.
		/// </summary>
		/// <param name="e">The exception to throw.</param>
		/// <remarks>If ExceptionsOnError is set to true then the specified Exception is thrown. 
		/// If ExceptionsOnError is set to false then the error message from the exception is output to standard output</remarks>
		protected void OnError(Exception e)
		{
			throw(e);
		}

		/// <summary>
		/// Called by the parser when a warning is generated.
		/// </summary>
		/// <param name="msg">The message associated with the warning</param>
		/// <exception cref="InvalidRdfException">ExceptionsOnWarnings is set to true</exception>
		protected void OnWarning(string msg)
		{
			Warnings.Add(msg);
			if(StopOnWarnings)
				OnWarning(new InvalidRdfException(msg));
		}

		/// <summary>
		/// Called by the parser when a warning is generated. 
		/// </summary>
		/// <param name="e">The exception to throw if ExceptionsOnWarnings is true</param>
		/// <remarks>If ExceptionsOnWarnings is set to true then the specified exception is thrown.
		/// If ExceptionsOnWarnings is set to false then the error message from the exception is output to standard output</remarks>
		protected void OnWarning(Exception e)
		{
			throw(e);
		}

		/// <summary>
		/// Adds an RdfNode object with the specified Uri to the Graph.
		/// </summary>
		/// <param name="nodeUri">The Uri of the node to add</param>
		/// <returns>The newly added RdfNode object</returns>
		/// <remarks>If the node with the specified Uri does not already exist it is created and added.
		/// If the node exists in the graph then a reference to the existing node is returned.
		/// If the specified Uri is null or blank the debug version asserts.
		/// If the specified Uri is not a well formed Uri then the debug version asserts.</remarks>
		protected RdfNode AddNodeToGraph(string nodeUri)
		{
			Debug.Assert(nodeUri != null);
			Debug.Assert(nodeUri.Length != 0);
			Debug.Assert(_rdfGraph != null);
			return (RdfNode)_rdfGraph.AddNode(nodeUri);
		}

		/// <summary>
		/// Adds an edge to the Graph
		/// </summary>
		/// <param name="edge">An object that implements the IRdfEdge interface and represents the new edge to add</param>
		protected void AddEdgeToGraph(IRdfEdge edge)
		{
			Debug.Assert(edge != null);
			Debug.Assert(_rdfGraph != null);
			Debug.Assert(edge.ID != null);
			Debug.Assert(edge.ID.Length != 0);
			_rdfGraph.Edges.Add(edge);
		}
		/// <summary>
		/// Adds an  RdfLiteral object to the RDF Graph     
		/// </summary>
		/// <param name="literalValue">The literal value of the node to add</param>
		/// <param name="langID">A string containing the language identifier for this Literal</param>
		/// <param name="datatypeUri">A string containing the URI that specifies the datatype for thsi literal.</param>
		/// <exception cref="UriFormatException">The specified datatypeUri  is not a null reference and is not a well formed URI</exception>
		/// <returns>The newly added RdfLiteral object</returns>
		/// <remarks>If a literal with the specified value, langID and datatype exists then a reference to the existing literal is returned. 
		/// If the Literal is not found a new one is created. Any of the supplied parameters, except literalValue, can be null.</remarks>
		protected RdfLiteral AddLiteralToGraph(string literalValue, string langID, string datatypeUri)
		{
			return (RdfLiteral)_rdfGraph.AddLiteral(literalValue,langID,datatypeUri);
		}

		/// <summary>
		/// Determines whether the specified URI string is a well formed URI
		/// </summary>
		/// <param name="uriString">A string</param>
		/// <returns>True if the specified string is a well formed URI.</returns>
		protected bool IsValidUri(string uriString)
		{
			if((uriString == null) || (uriString.Length == 0))
				return false;
			try
			{
				Uri u = new Uri(uriString);
				return true;
			}
			catch(UriFormatException)
			{
				return false;
			}
		}
	}
}
