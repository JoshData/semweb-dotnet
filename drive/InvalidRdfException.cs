/*****************************************************************************
 * InvalidRdfException.cs
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
	/// Represents an exception that is thrown when invalid RDF Syntax is encountered by the parser
	/// </summary>
	public class InvalidRdfException : Exception
	{
		private string _message;
		/// <summary>
		/// Gets a message describing the exeption or an empty string
		/// </summary>
		public override string Message
		{
			get
			{
				return _message;
			}
		}
		/// <summary>
		/// Initializes a new instance of the InvalidRdfException class with an empty message
		/// </summary>
		public InvalidRdfException()
		{
			_message = "";
		}

		/// <summary>
		/// Initializes a new instance of the InvalidRdfException class with the given message
		/// </summary>
		/// <param name="message">A string describing a reason for the exception</param>
		public InvalidRdfException(string message)
		{
			_message = message;
		}
	}
}
