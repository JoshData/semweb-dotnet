/*****************************************************************************
 * RdfLiteral.cs
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
	/// Represents a Literal in the RDF Graph.
	/// </summary>
	/// <remarks>A literal is uniquely identified by its value, LanguageID and Datatype URI. 
	/// The ID of a Literal is composed of a concatenated string of these three value. If the Datatype and Language ID are 
	/// not specified then they are assumed to be null (default for the locale or data) and the ID is set to the value.</remarks>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfLiteral : RdfNode, IRdfLiteral
	{
		/// <summary>
		/// The Datatype URI of this Literal
		/// </summary>
		private Uri _dataType;

		/// <summary>
		/// Gets or sets the Datatype URI of this Literal
		/// </summary>
		/// <exception cref="UriFormatException">Attempt to set the Datatype to a URI string that is not a well formed URI.</exception>
		public string Datatype
		{
			get
			{
				if(_dataType == null)
					return "";
				return _dataType.ToString();
			}
			set
			{
				if(_dataType == null)
                    _dataType = new Uri(value);
			}
		}
		/// <summary>
		/// The value for this literal.
		/// </summary>
		private string _literalValue;

		/// <summary>
		/// Gets or sets the value of this Literal.
		/// </summary>
		public string Value
		{
			get
			{
				return _literalValue;
			}
			set
			{
				_literalValue = value;
			}
		}
		
		/// <summary>
		/// Gets the ID of this literal
		/// </summary>
		/// <exception cref="ArgumentException">Attempt to set the ID of this literal.</exception>
		/// <remarks>This is a string composed of the Value, LanguageID and the Datatype URI.
		/// You cannot use this property to set the ID of a literal.</remarks>
		public override string ID
		{
			get
			{
				string literalID = _literalValue;
				if((LangID != null) && (LangID.Length != 0))
					literalID+="@"+LangID;
				if(_dataType != null)
					literalID +="^^"+_dataType.ToString();
				return literalID;
			}
			set
			{
				throw(new NotSupportedException("Cannot directly set the ID of an RdfLiteral"));
			}
		}

		/// <summary>
		/// Initializes a new instance of the RdfLiteral class.
		/// </summary>
		/// <remarks>Sets the Datatype URI to null and the Value and the Language ID to empty strings.</remarks>
		public RdfLiteral()
		{
			_dataType = null;
		}

		/// <summary>
		/// Initializes a new instance of the RdfLiteral class.
		/// </summary>
		/// <param name="literalValue">A string representing the value of this Literal.</param>
		/// <remarks>Sets the Datatype URI to null and the Labguage ID to an empty string.</remarks>
		public RdfLiteral(string literalValue)
		{
			_dataType = null;
			_literalValue = literalValue;
		}

		/// <summary>
		/// Initializes a new instance if the RdfLiteral class.
		/// </summary>
		/// <param name="literalValue">A string representing the value of this literal.</param>
		/// <param name="languageID">A string representing the Language ID of this literal.</param>
		/// <param name="datatypeUri">A string representing the Datatype URI of this Literal.</param>
		/// <exception cref="UriFormatException">The specified datatypeUri is not null and is not a well formed URI.</exception>
		public RdfLiteral(string literalValue, string languageID, string datatypeUri)
		{
			if(datatypeUri != null)
				_dataType = new Uri(datatypeUri);
			_literalValue = literalValue;
			LangID = languageID;
		}

		/// <summary>
		/// Returns an N-Triple representation of this literal
		/// </summary>
		/// <returns>A string containing the N-Triple representation of this literal.</returns>
		public override string ToNTriple()
		{
			StringBuilder literal = new StringBuilder(_literalValue.Replace("\"","\\\""));
			literal.Replace("\x0d\x0a","\x0a");
			literal.Replace("\x0a","\\n");
			literal.Insert(0,"\"");
			literal.Append("\"");
			if((LangID != null) && (LangID.Length != 0))
				literal.Append("@"+LangID);
			if(_dataType != null)
				literal.Append("^^<"+_dataType.ToString()+">");

			return Util.ToCharmod(literal.ToString());
		}

		/// <summary>
		/// Gets the string representation of this literal
		/// </summary>
		/// <returns>A string containg this literal</returns>
		public override string ToString()
		{
			return ID;
		}
	}
}
