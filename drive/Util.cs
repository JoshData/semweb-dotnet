/*****************************************************************************
 * Util.cs
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
using System.Text;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// Represents a class of utility methods
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	public class Util
	{
		/// <summary>
		/// Prints all the nodes in the specified RDF Graph to the console
		/// </summary>
		/// <param name="graph">An object that impplements the IRdfGraph interface</param>
		public static void PrintNodes(IRdfGraph graph)
		{
			if(graph == null)
				throw(new ArgumentNullException());
			Print(graph.Nodes);
		}

		/// <summary>
		/// Prints all the edges in the specified RDF Graph to the console
		/// </summary>
		/// <param name="graph">An object that impplements the IRdfGraph interface</param>
		public static void PrintEdges(IRdfGraph graph)
		{
			if(graph == null)
				throw(new ArgumentNullException());
			Print(graph.Edges);
		}
		/// <summary>
		/// Prints all the Literals in the specified RDF Graph to the console
		/// </summary>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		public static void PrintLiterals(IRdfGraph graph)
		{
			if(graph == null)
				throw(new ArgumentNullException());
			Print(graph.Literals);
		}

		/// <summary>
		/// Prints all the N-Triples in the specified RDF Graph to the console
		/// </summary>
		/// <param name="graph">An object that implements the IRdfGraph interface</param>
		public static void PrintNTriples(IRdfGraph graph)
		{
			if(graph == null)
				throw(new ArgumentNullException());
			IDictionaryEnumerator enumerator = (IDictionaryEnumerator)graph.Nodes.GetEnumerator();
			while(enumerator.MoveNext())
			{
				IRdfNode parentNode = (IRdfNode)enumerator.Value;
				foreach(IRdfEdge childEdge in parentNode.ChildEdges)
				{		
					Console.WriteLine(parentNode.ToNTriple()+"  "+childEdge.ToNTriple()+"  "+childEdge.ChildNode.ToNTriple());
				}
			}
		}

		/// <summary>
		/// Prints all the nodes in the specified Node Collection to the console
		/// </summary>
		/// <param name="nodeCollection">An object that implements the IRdfNodeCollection interface</param>
		public static void Print(IRdfNodeCollection nodeCollection)
		{
			if(nodeCollection == null)
				throw(new ArgumentNullException());
			IDictionaryEnumerator enumerator = (IDictionaryEnumerator)nodeCollection.GetEnumerator();
			while(enumerator.MoveNext())
			{
				Console.WriteLine(((IRdfNode)(enumerator.Value)).ID);
			}
		}

		/// <summary>
		/// Prints all the edges in the specified Edge Collection to the console
		/// </summary>
		/// <param name="edgeCollection">An object that implements the IRdfEdgeCollection interface</param>
		public static void Print(IRdfEdgeCollection edgeCollection)
		{
			if(edgeCollection == null)
				throw(new ArgumentNullException());
			foreach(IRdfEdge edge in edgeCollection)
			{
				Console.WriteLine(edge.ID);
			}
		}

		/// <summary>
		/// Prints the specified RDF Node to the console as an N-Triple
		/// </summary>
		/// <param name="node">An object that implements the IRdfNode interface</param>
		public static void PrintNTriple(IRdfNode node)
		{
			Console.WriteLine(node.ToNTriple());
		}

		/// <summary>
		/// Prints the specified RDF Edge to the console as an N-Triple
		/// </summary>
		/// <param name="edge">An object that implements thet IRdfEdge interface</param>
		public static void PrintNTriple(IRdfEdge edge)
		{
			Console.WriteLine(edge.ToNTriple());
		}

		/// <summary>
		/// Returns the Charmod escaped representation of this string
		/// </summary>
		/// <param name="str">A string</param>
		/// <returns>A string with the Charmod escaped representation</returns>
		public static string ToCharmod(string str)
		{
			Debug.Assert(str != null);
			StringBuilder charmodStrBldr = new StringBuilder();
			for(int i=0;i<str.Length;i++)
			{
				int charcode = (int)str[i];
				if(charcode > 127)
					charmodStrBldr.AppendFormat("\\u{0:X4}",charcode);
				else
					charmodStrBldr.Append(str[i]);
			}
			return charmodStrBldr.ToString();
		}
	}
}
