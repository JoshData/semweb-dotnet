/*****************************************************************************
 * RdfContainer.cs
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
	/// Represents an RDF Container
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public abstract class RdfContainer : RdfNode, IRdfNode
	{
		/// <summary>
		/// The child edge that connects to a node specifying the type of the container
		/// </summary>
		protected RdfEdge _typeEdge;

		/// <summary>
		/// Gets or sets the node that specifies the type of this container
		/// </summary>
		/// <exception cref="ArgumentNullException">The specified value id null.</exception>
		public IRdfEdge Type
		{
			get
			{
				return _typeEdge;
			}
		}
	}
}
