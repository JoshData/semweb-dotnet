/*****************************************************************************
 * RdfNTriple.cs
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
	/// Summary description for RdfNTriple.
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class RdfNTriple : IRdfNTriple
	{
		/// <summary>
		/// The subject of this Triple
		/// </summary>
		private string _subject;

		/// <summary>
		/// Gets or Sets the subject of this Triple
		/// </summary>
		public string Subject
		{
			get
			{
				return _subject;
			}
			set
			{
				_subject = value;
			}
		}

		/// <summary>
		/// The predicate of this Triple
		/// </summary>
		private string _predicate;
		
		/// <summary>
		/// Gets or Sets the predicate of this Triple
		/// </summary>
		public string Predicate
		{
			get
			{
				return _predicate;
			}
			set
			{
				_predicate = value;
			}
		}

		/// <summary>
		/// The object of this Triple
		/// </summary>
		private string _object;

		/// <summary>
		/// Gets or Sets the object of this Triple
		/// </summary>
		public string Object
		{
			get
			{
				return _object;
			}
			set
			{
				_object = value;
			}
		}
		
		/// <summary>
		/// Initializes a new instance of the RdfNtriple class
		/// </summary>
		public RdfNTriple()
		{
			_subject = "";
			_predicate = "";
			_object = "";
		}

		/// <summary>
		/// Initializes a new instance of the RdfNtriple class
		/// </summary>
		/// <param name="subject">The subject of the new triple</param>
		/// <param name="predicate">The predicate of the new triple</param>
		/// <param name="obj">The object of the new triple</param>
		public RdfNTriple(string subject, string predicate, string obj)
		{
			_subject = subject;
			_predicate = predicate;
			_object = obj;
		}
	}
}
