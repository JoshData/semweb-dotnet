/*****************************************************************************
 * IRdfParserFactory.cs
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
	/// Represents a class that generates RDF Parser objects
	/// </summary>
	public interface IRdfParserFactory
	{
		/// <summary>
		/// When implemented by a class, creates and returns an RDF/XML Parser
		/// </summary>
		/// <returns>An object that implements the IRdfParser interface</returns>
		IRdfXmlParser GetRdfXmlParser();

		/// <summary>
		/// When implemented by a class, creates and returns an RDF NTriples Parser
		/// </summary>
		/// <returns>An object that implements the IRdfParser interface</returns>
		IRdfN3Parser GetRdfN3Parser();
	}
}
