/*****************************************************************************
 * IRdfXmlParser.cs
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
using System.IO;
using System.Xml;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents an RDF/XML parser
	/// </summary>
	public interface IRdfXmlParser : IRdfParser
	{
		/// <summary>
		/// When implemented by a class, parses the RDF from the given TextReader, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(TextReader txtReader, IRdfGraph graph, string xmlbaseUri);
		
		/// <summary>
		/// When implemented by a class, parses the RDF from the given TextReader, using the given xml:base uri
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(TextReader txtReader, string xmlbaseUri);

		/// <summary>
		/// When implemented by a class, parses the RDF from the given TextReader, into an existing graph
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(TextReader txtReader, IRdfGraph graph);
		
		/// <summary>
		/// When implemented by a class, parses the RDF from the given TextReader
		/// </summary>
		/// <param name="txtReader">The TextReader to use as the source of the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(TextReader txtReader);
		

		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlReader, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlReader reader, IRdfGraph graph, string xmlbaseUri);


		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlReader, using the given xml:base uri
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlReader reader, string xmlbaseUri);

		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlReader, into an existing graph
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlReader reader, IRdfGraph graph);

		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlReader
		/// </summary>
		/// <param name="reader">The XmlReader to use as the source of the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlReader reader);


		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlDocument, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlDocument doc, IRdfGraph graph, string xmlbaseUri);

		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlDocument, using the given xml:base uri
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlDocument doc, string xmlbaseUri);

		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlDocument, into an existing graph
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlDocument doc, IRdfGraph graph);

		/// <summary>
		/// When implemented by a class, parses the RDF from the given XmlDocument
		/// </summary>
		/// <param name="doc">The XmlDocument to use as the source of the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(XmlDocument doc);
		
		/// <summary>
		/// When implemented by a class, parses the RDF from the given stream, into an existing graph using the given xml:base uri
		/// </summary>
		/// <param name="inStream">The Stream to use as the source of the XML data</param>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data or the graph</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(Stream inStream, IRdfGraph graph, string xmlbaseUri);

		/// <summary>
		/// When implemented by a class, parses the RDF from the given stream, using the given xml:base uri
		/// </summary>
		/// <param name="inStream">The Stream to use as the source of the XML data</param>
		/// <param name="xmlbaseUri">The xml:base Uri to use incase one is not found in the XML data</param>
		/// <returns>An object that implements the IRdfGraph interface</returns>
		IRdfGraph ParseRdf(Stream inStream, string xmlbaseUri);
	}
}
