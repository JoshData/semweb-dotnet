using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Drive.Rdf
{
	/// <summary>
	/// 
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	internal class ReadOnlyRdfEdgeList : RdfEdgeList
	{
		public override IRdfEdge this[int index]
		{
			get
			{
				return (RdfEdge)_edges[index];
			}
			set
			{
				throw (new NotSupportedException("Operation not supported. Collection is Read Only."));
			}
		}
		public override void Add(IRdfEdge newEdge)
		{
			throw (new NotSupportedException("Operation not supported. Collection is Read Only."));
		}
		public override void Remove(IRdfEdge edge)
		{
			throw (new NotSupportedException("Operation not supported. Collection is Read Only."));
		}
		public override void RemoveAll()
		{
			throw (new NotSupportedException("Operation not supported. Collection is Read Only."));
		}
		internal ReadOnlyRdfEdgeList(RdfEdgeList edgeList)
		{
			Debug.Assert(edgeList != null);
			_edges = edgeList.EdgeList;
		}
	}
}
