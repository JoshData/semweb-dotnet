using System;
using System.Collections;

namespace SemWeb {
	
	public class NamespaceManager {
		NamespaceManager parent;
		Hashtable atob = new Hashtable();
		Hashtable btoa = new Hashtable();
		
		public NamespaceManager() : this (null) {
		}
		
		public NamespaceManager(NamespaceManager parent) {
			this.parent = parent;
		}
		
		public void AddNamespace(string uri, string prefix) {
			atob[uri] = prefix;
			btoa[prefix] = uri;
		}
		
		public virtual string GetNamespace(string prefix) {
			string ret = (string)btoa[prefix];
			if (ret != null) return ret;
			if (parent != null) return parent.GetNamespace(prefix);
			return null;
		}
		
		public virtual string GetPrefix(string uri) {
			string ret = (string)atob[uri];
			if (ret != null) return ret;
			if (parent != null) return parent.GetPrefix(uri);
			return null;
		}
		
		public bool Normalize(string uri, out string prefix, out string localname) {
			int hash = uri.LastIndexOf('#');
			if (hash > 0) {
				prefix = GetPrefix(uri.Substring(0, hash+1));
				if (prefix != null) {
					localname = uri.Substring(hash+1);
					return true;
				}
			}
			
			hash = uri.LastIndexOf('/');
			if (hash > 0) {
				prefix = GetPrefix(uri.Substring(0, hash+1));
				if (prefix != null) {
					localname = uri.Substring(hash+1);
					return true;
				}
			}
			
			prefix = null;
			localname = null;
			
			return false;
		}
		
		public string Normalize(string uri) {
			string prefix, localname;
			if (Normalize(uri, out prefix, out localname))
				return prefix + ":" + localname;
			return "<" + uri + ">";
		}
		
		public string Resolve(string qname) {
			int colon = qname.IndexOf(':');
			if (colon == -1) throw new ArgumentException("Invalid qualified name.");
			return GetNamespace(qname.Substring(0, colon)) + qname.Substring(colon+1);
		}
		
		public ICollection GetNamespaces() {
			return atob.Keys;
		}

		public ICollection GetPrefixes() {
			return atob.Values;
		}
	}
}

namespace SemWeb.IO {
	using SemWeb;
	
	public class AutoPrefixNamespaceManager : NamespaceManager {
		int counter = 0;
		
		public AutoPrefixNamespaceManager() : this (null) {
		}
		
		public AutoPrefixNamespaceManager(NamespaceManager parent) : base(parent) {
		}
		
		public override string GetPrefix(string uri) {
			string ret = base.GetPrefix(uri);
			if (ret != null) return ret;
			
			if (uri == "http://www.w3.org/1999/02/22-rdf-syntax-ns#" && GetNamespace("rdf") == null)
				ret = "rdf";
			else if (uri == "http://www.w3.org/2000/01/rdf-schema#" && GetNamespace("rdfs") == null)
				ret = "rdfs";
			else if (uri == "http://www.w3.org/2002/07/owl#" && GetNamespace("owl") == null)
				ret = "owl";
			else if (uri == "http://purl.org/dc/elements/1.1/" && GetNamespace("dc") == null)
				ret = "dc";
			else if (uri == "http://xmlns.com/foaf/0.1/" && GetNamespace("foaf") == null)
				ret = "foaf";
			else			
				ret = "autons" + (counter++);
			AddNamespace(uri, ret);
			return ret;
		}
	}
}
