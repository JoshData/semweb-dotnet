using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

using SemWeb;
using SemWeb.Stores;

public class Driver {
	public static void Main(string[] args) {
		if (args.Length < 3) {
			Console.Error.WriteLine("Usage:  mono rdfbind.exe bindings.txt targetschema schmefile1 schemafile2 . . .");
			return;
		}
		
		// Parse command-line arguments
		string bindingmapfile = args[0];
		string targetschema = args[1];
		ArrayList schemafiles = new ArrayList();
		for (int i = 2; i < args.Length; i++)
			schemafiles.Add(args[i]);
		
		// Load the binding map
		Hashtable bindingmap = new Hashtable();
		ArrayList schemalist = new ArrayList();
		try {
			char[] whitespacechars = { ' ', '\t' };
			using (TextReader map = new StreamReader(bindingmapfile)) {
				string line;
				while ((line = map.ReadLine()) != null) {
					if (line == "" || line.StartsWith("#")) continue;
					int whitespace = line.IndexOfAny(whitespacechars);
					if (whitespace == -1)
						throw new FormatException("Each line should be an assembly/namespace name followed by a space or tab, followed by a schema URI.");
					string name = line.Substring(0, whitespace).Trim();
					string uri = line.Substring(whitespace+1).Trim();
					bindingmap[uri] = name;
					schemalist.Add(uri);
					
					// Let targetscheme be either a name or URI.
					if (targetschema == name)
						targetschema = uri;
				}
			}
		} catch (Exception e) {
			Console.Error.WriteLine("Error loading the binding map: " + e.Message);
			return;
		}
		
		if (!bindingmap.ContainsKey(targetschema)) {
			Console.Error.WriteLine("The target schema must have an entry in the binding map.");
			return;
		}
		
		MultiStore schemas = new MultiStore();
		foreach (string schemafile in schemafiles) {
			try {
				Store schema = new MemoryStore(new RdfXmlReader(schemafile));
				schemas.Add(schema);
			} catch (Exception e) {
				Console.Error.WriteLine("Error loading the schema in '" + schemafile + "': " + e.Message);
				return;
			}
		}
		
		foreach (string sch in schemalist) {
			AssemblyBuilder a = new SemWeb.Bind.Bindings(sch, schemas, bindingmap).CreateBindings();
			a.Save((string)bindingmap[sch] + ".dll");
		}

	}
}

namespace SemWeb.Bind {
	public class Bindings {
		private const string RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
		private const string RDFS = "http://www.w3.org/2000/01/rdf-schema#";
		
		string targetschemauri;
		Store schemas;
		Hashtable bindingmap;
		
		string ns;
		
		AssemblyBuilder a;
		ModuleBuilder m;
		
		Hashtable definedclasses = new Hashtable();
		Hashtable definedconstructors = new Hashtable();
		
		Hashtable classtypes = new Hashtable();
		
		static Type[] constructorargs = new Type[] { typeof(Entity), typeof(Store) };
		static Type[] arraylisttoarrayargs = new Type[] { typeof(Type) };
		
		static Type[] newentityargs = new Type[] { typeof(string) };
		
		static Type[] selectsubjectsargs = new Type[] { typeof(Entity), typeof(Resource) };
		static Type[] selectobjectsargs = new Type[] { typeof(Entity), typeof(Entity) };
		
		ConstructorInfo anyconstructor = typeof(Any).GetConstructor(constructorargs);
		ConstructorInfo arraylistconstructor = typeof(ArrayList).GetConstructor(Type.EmptyTypes);
		MethodInfo arraylisttoarray = typeof(ArrayList).GetMethod("ToArray", arraylisttoarrayargs);
		MethodInfo arraylistadd = typeof(ArrayList).GetMethod("Add", new Type[] { typeof(object) });
		
		MethodInfo gettypefromhandle = typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) });
		
		ConstructorInfo newentity = typeof(Entity).GetConstructor(newentityargs);
		ConstructorInfo newliteral = typeof(Literal).GetConstructor(newentityargs);
		
		MethodInfo getentity = typeof(Any).GetProperty("Entity").GetGetMethod();
		MethodInfo getmodel = typeof(Any).GetProperty("Model").GetGetMethod();			
		
		MethodInfo selectsubjects = typeof(Store).GetMethod("SelectSubjects", selectsubjectsargs);
		MethodInfo selectobjects = typeof(Store).GetMethod("SelectObjects", selectobjectsargs);
		
		MethodInfo literalvalue = typeof(Literal).GetProperty("Value").GetGetMethod();

		MethodInfo anyaddvalue = typeof(Any).GetMethod("AddValue", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Entity), typeof(object), typeof(bool)}, null);
		MethodInfo anyremvalue = typeof(Any).GetMethod("RemoveValue", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Entity), typeof(object), typeof(bool)}, null);
		MethodInfo anysetfuncvalue = typeof(Any).GetMethod("SetFuncProperty", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Entity), typeof(object), typeof(bool)}, null);
		
		public Bindings(string targetschemauri, Store schemas, Hashtable bindingmap) {
			this.targetschemauri = targetschemauri;
			this.schemas = schemas;
			this.bindingmap = bindingmap;
		}
		
		public AssemblyBuilder CreateBindings() {
			ns = (string)bindingmap[targetschemauri];
		
			AssemblyName n = new AssemblyName();
			n.Name = ns;
			
			a = AppDomain.CurrentDomain.DefineDynamicAssembly(n, AssemblyBuilderAccess.RunAndSave);
			m = a.DefineDynamicModule("Main", ns + ".dll");
			
			// Define each class in the schema
			Entity[] classes = schemas.SelectSubjects(RDF + "type", (Entity)(RDFS + "Class"));
			foreach (Entity c in classes) {
				if (c.Uri != null && c.Uri.StartsWith(targetschemauri)
					|| schemas.Contains(new Statement(c, RDFS + "isDefinedBy", (Entity)targetschemauri)))
				DefineClass(c);
			}
			
			foreach (Entity c in definedclasses.Keys)
				DefineClassMethods(c, (TypeBuilder)definedclasses[c]);

			return a;
		}
		
		private string MakeName(string name) {
			string ret = "";
			foreach (char c in name) {
				if (char.IsLetterOrDigit(c))
					ret += ret.Length == 0 ? char.ToUpper(c) : c;
			}
			return ret;
		}
		
		private string GetLocalName(Entity e, string definingschema) {
			if (e.Uri != null && e.Uri.StartsWith(definingschema))
				return MakeName(e.Uri.Substring(definingschema.Length));
				
			foreach (Resource r in schemas.SelectObjects(e, RDFS + "label"))
				if (r is Literal)
					return MakeName(((Literal)r).Value);
					
			return null;
		}
		
		private string GetDefiningSchema(Entity e) {
			foreach (Resource r in schemas.SelectObjects(e, RDFS + "isDefinedBy"))
				if (r is Entity && r.Uri != null)
					return r.Uri;
			foreach (string schema in bindingmap.Keys)
				if (e.Uri != null && e.Uri.StartsWith(schema))
					return schema;
			return null;
		}
		
		private Type GetType(Entity e) {
			if (definedclasses.ContainsKey(e)) return (Type)definedclasses[e];
			if (classtypes.ContainsKey(e)) return (Type)classtypes[e];
			
			if (e.Uri != null) {
				if (e.Uri == "http://www.w3.org/2000/01/rdf-schema#Literal"
					|| e.Uri.StartsWith("http://www.w3.org/2001/XMLSchema#"))
					return typeof(string);
			}
			
			string schema = GetDefiningSchema(e);
			if (schema == null && e.Uri != null) Console.Error.WriteLine("Schema of " + e.Uri + " unknown.");
			if (schema == null) return null;
			
			string localname = GetLocalName(e, schema);
			if (schema == null && e.Uri != null) Console.Error.WriteLine("Local name of " + e.Uri + " could not be determined.");
			if (localname == null) return null;
			
			string ns = (string)bindingmap[schema];
			if (ns == null) Console.Error.WriteLine("Schema " + schema + " is not in the binding map.");
			if (ns == null) return null;
			
			string name = ns + "." + localname;

			try {
				Assembly b = Assembly.LoadFrom(ns + ".dll");
				if (b == null) throw new Exception("Assembly " + ns + " not found.");
				
				Type t = b.GetType(name);
				classtypes[e] = t;
				return t;
			} catch (Exception ex) {
				Console.Error.WriteLine("Could not load type " + name + ": " + ex.Message);
				return null;
			}
		}
	
		private void DefineClass(Entity c) {
			string ln = GetLocalName(c, targetschemauri);
			if (ln == null) return;
			
			string typename = ns + "." + ln;
			Console.WriteLine(c + "\t" + typename);
			
			TypeBuilder t = m.DefineType(typename,
				TypeAttributes.Class | TypeAttributes.Public, typeof(Any));
			definedclasses[c] = t;
			
			// Constructor
			ILGenerator il;
			ConstructorBuilder constructor = t.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, constructorargs);
			definedconstructors[t] = constructor;
			il = constructor.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0); // this
			il.Emit(OpCodes.Ldarg_1); // entity
			il.Emit(OpCodes.Ldarg_2); // model
			il.Emit(OpCodes.Call, anyconstructor);
			il.Emit(OpCodes.Ret);
		}
		
		private ConstructorInfo GetConstructor(Type t) {
			if (definedconstructors.ContainsKey(t))
				return (ConstructorInfo)definedconstructors[t];
			else {
				return t.GetConstructor(constructorargs);
			}
		}
		
		private void DefineClassMethods(Entity c, TypeBuilder t) {
			ILGenerator il;
			
			// Conversion operations to super classes
			foreach (Resource r in schemas.SelectObjects(c, RDFS + "subClassOf")) {
				if (!(r is Entity)) continue;
				Type superclass = GetType((Entity)r);
				if (superclass == null || superclass == typeof(string)) continue;
				
				string methodname;
				if (superclass.Assembly == a)
					methodname = superclass.Name;
				else
					methodname = superclass.FullName.Replace(".", "");
				
				MethodBuilder method = t.DefineMethod("As" + methodname, MethodAttributes.Public, superclass, Type.EmptyTypes);
				il = method.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0); // this
				il.Emit(OpCodes.Call, getentity);
				il.Emit(OpCodes.Ldarg_0); // this
				il.Emit(OpCodes.Call, getmodel);
				
				ConstructorInfo superconstructor = GetConstructor(superclass);
				il.Emit(OpCodes.Newobj, superconstructor);
				il.Emit(OpCodes.Ret);
			}
			
			// Properties
			foreach (Entity p in schemas.SelectSubjects(RDFS + "domain", c))
				DefineProperty(c, p, t, true, true);
				
			foreach (Entity p in schemas.SelectSubjects(RDFS + "domain", (Entity)"http://www.w3.org/2000/01/rdf-schema#Resource")) {
				if (GetDefiningSchema(p) != targetschemauri) continue;
				DefineProperty(c, p, t, true, false);
			}
			
			foreach (Entity p in schemas.SelectSubjects(RDFS + "range", c))
				DefineProperty(c, p, t, false, true);
			
			t.CreateType();
		}
		
		private void DefineProperty(Entity c, Entity p, TypeBuilder t, bool forward, bool actualDomain) {
			if (p.Uri == null) return;		
		
			bool functional = false;
			if (forward && schemas.Contains(new Statement(p, RDF + "type", (Entity)"http://www.w3.org/2002/07/owl#FunctionalProperty")))
				functional = true;
			if (!forward && schemas.Contains(new Statement(p, RDF + "type", (Entity)"http://www.w3.org/2002/07/owl#InverseFunctionalProperty")))
				functional = true;
			
			string propschema = GetDefiningSchema(p);
			if (propschema == null) return;
			
			string proplocalname = GetLocalName(p, propschema);
			if (proplocalname == null) return;
			
			string propname;
			if (propschema == targetschemauri)
				propname = proplocalname;
			else if (!bindingmap.ContainsKey(propschema))
				return;
			else
				propname = ((string)bindingmap[propschema]).Replace(".", "") + proplocalname;
				
			if (!forward) {
				if (propname.EndsWith("Of")) {
					propname = propname.Substring(0, propname.Length-2);
				} else {
					if (propname.StartsWith("has"))
						propname = propname.Substring(3);
					propname += "Of";
				}
			}
			
			Resource[] range = schemas.SelectObjects(p, RDFS + (forward ? "range" : "domain"));
			Type rettype = null;
			if (range.Length == 1 && range[0] is Entity)
				rettype = GetType((Entity)range[0]);
			if (rettype == null)
				rettype = typeof(Any);
			Type retelemtype = rettype;
			if (!functional)
				rettype = rettype.Assembly.GetType(rettype.FullName + "[]");
				
			PropertyBuilder property = t.DefineProperty(propname, PropertyAttributes.None, rettype, Type.EmptyTypes);

			DefinePropertyGetter(c, p, t, forward, propname, rettype, retelemtype, functional, property);
			DefinePropertySetter(c, p, t, forward, propname, rettype, retelemtype, functional, property);
			DefinePropertyAddRemove(c, p, t, forward, propname, rettype, retelemtype, functional, property);
			if (actualDomain && retelemtype == typeof(string))
				DefinePropertyStaticConstructor(c, p, t, propname);
		}
			
		private void DefinePropertyGetter(Entity c, Entity p, TypeBuilder t, bool forward, string propname, Type rettype, Type retelemtype, bool functional, PropertyBuilder property) {
			MethodBuilder method = t.DefineMethod("get_" + propname,
				MethodAttributes.Public | MethodAttributes.SpecialName,
				CallingConventions.HasThis,
				rettype, Type.EmptyTypes);
			property.SetGetMethod(method);
			
			ILGenerator il = method.GetILGenerator();
			
			il.DeclareLocal(typeof(ArrayList));
			il.DeclareLocal(typeof(int));
			if (retelemtype == typeof(string))
				il.DeclareLocal(typeof(string));
			else
				il.DeclareLocal(typeof(Entity));

			if (!functional) {
				// Create array list in local variable 0
				il.Emit(OpCodes.Newobj, arraylistconstructor);
				il.Emit(OpCodes.Stloc_0);
			}
			
			// Push model (arg 0 of Select)
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, getmodel);
			
			if (forward) {
				// Push entity (arg 1 to Select)
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Call, getentity);
				
				// Push property (arg 2 to Select)
				il.Emit(OpCodes.Ldstr, p.Uri);
				il.Emit(OpCodes.Newobj, newentity);
			} else {
				// Push property (arg 1 to Select)
				il.Emit(OpCodes.Ldstr, p.Uri);
				il.Emit(OpCodes.Newobj, newentity);
				
				// Push entity (arg 2 to Select)
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Call, getentity);
			}
			
			// Call select (pushes Resource[] or Entity[])
			
			if (forward)
				il.Emit(OpCodes.Call, selectobjects);
			else
				il.Emit(OpCodes.Call, selectsubjects);
			
			// Loop through the statements
			
			Label loopstart = il.DefineLabel();
			Label loopend = il.DefineLabel();
			Label loopcontinue = il.DefineLabel();
			il.Emit(OpCodes.Ldc_I4_0); // initialize counter local var 1
			il.Emit(OpCodes.Stloc_1);
			
			il.MarkLabel(loopstart);
			il.Emit(OpCodes.Dup); // dup the array
			il.Emit(OpCodes.Ldlen); // push length of array
			il.Emit(OpCodes.Ldloc_1); // push counter
			il.Emit(OpCodes.Ble, loopend);
			
			il.Emit(OpCodes.Dup); // dup the array
			il.Emit(OpCodes.Ldloc_1); // push counter
			il.Emit(OpCodes.Ldelem_Ref);	// pop array, counter, push element
			
			// Ensure value is of the right type
			if (retelemtype == typeof(string)) {
				// Literal value
				il.Emit(OpCodes.Isinst, typeof(Literal));
				il.Emit(OpCodes.Brfalse, loopcontinue);
			} else if (forward) { // if !forward, it must be an entity
				// Ensure entity value
				il.Emit(OpCodes.Isinst, typeof(Entity));
				il.Emit(OpCodes.Brfalse, loopcontinue);
			}
			
			// Because of the br, we've lost the object reference -- load it again
			
			il.Emit(OpCodes.Dup); // dup the array
			il.Emit(OpCodes.Ldloc_1); // push counter
			il.Emit(OpCodes.Ldelem_Ref);	// pop array, counter, push element
			
			// Get the value we want to return
			if (retelemtype == typeof(string)) {
				// Cast to literal, replace it with its value
				il.Emit(OpCodes.Castclass, typeof(Literal));
				il.Emit(OpCodes.Call, literalvalue);
			} else {
				// Cast to entity, push model, replace with bound type
				il.Emit(OpCodes.Castclass, typeof(Entity));
				
				il.Emit(OpCodes.Ldarg_0); // model
				il.Emit(OpCodes.Call, getmodel);

				ConstructorInfo ctor = GetConstructor(retelemtype);
				il.Emit(OpCodes.Newobj, ctor);			
			}
			
			if (!functional) {
				// We need the ArrayList before this argument.
				il.Emit(OpCodes.Stloc_2);
				il.Emit(OpCodes.Ldloc_0); // push ArrayList
				il.Emit(OpCodes.Ldloc_2); // get back the object
				il.Emit(OpCodes.Call, arraylistadd); // add to ArrayList
				il.Emit(OpCodes.Pop); // pop the int32 return value of Add
			} else {
				// Store the result, clear the stack, get the result back, and return.
				il.Emit(OpCodes.Stloc_2);
				il.Emit(OpCodes.Pop); // the entities array
				il.Emit(OpCodes.Ldloc_2);
				il.Emit(OpCodes.Ret);
			}
			
			// increment counter
			il.MarkLabel(loopcontinue);
			il.Emit(OpCodes.Ldloc_1); // push counter
			il.Emit(OpCodes.Ldc_I4_1); // push 1
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stloc_1);
			
			// go to start of loop
			il.Emit(OpCodes.Br, loopstart);
			il.MarkLabel(loopend);
			il.Emit(OpCodes.Pop); // pop the Resource[] array
			
			if (!functional) {
				// Load array list and convert to array, and return.
				il.Emit(OpCodes.Ldloc_0);
				il.Emit(OpCodes.Ldtoken, retelemtype);
				il.Emit(OpCodes.Call, gettypefromhandle);
				il.Emit(OpCodes.Call, arraylisttoarray);
				il.Emit(OpCodes.Castclass, rettype);
			} else {
				il.Emit(OpCodes.Ldnull);
			}
			
			il.Emit(OpCodes.Ret);
		}
		
		private void DefinePropertySetter(Entity c, Entity p, TypeBuilder t, bool forward, string propname, Type rettype, Type retelemtype, bool functional, PropertyBuilder property) {
			if (!functional) return;
		
			MethodBuilder method = t.DefineMethod("set_" + propname,
				MethodAttributes.Public | MethodAttributes.SpecialName,
				CallingConventions.HasThis,
				null, new Type[] { rettype } );
			property.SetSetMethod(method);
			
			ILGenerator il = method.GetILGenerator();
			
			il.Emit(OpCodes.Ldarg_0);

			il.Emit(OpCodes.Ldstr, p.Uri);
			il.Emit(OpCodes.Newobj, newentity);

			il.Emit(OpCodes.Ldarg_1);
			
			if (forward)
				il.Emit(OpCodes.Ldc_I4_1);
			else
				il.Emit(OpCodes.Ldc_I4_0);
			
			il.Emit(OpCodes.Call, anysetfuncvalue);
			il.Emit(OpCodes.Ret);
		}

		private void DefinePropertyAddRemove(Entity c, Entity p, TypeBuilder t, bool forward, string propname, Type rettype, Type retelemtype, bool functional, PropertyBuilder property) {
			if (functional) return;
			DefinePropertyAddRemove(c, p, t, forward, propname, rettype, retelemtype, functional, property, true);
			DefinePropertyAddRemove(c, p, t, forward, propname, rettype, retelemtype, functional, property, false);
		}
		
		private void DefinePropertyAddRemove(Entity c, Entity p, TypeBuilder t, bool forward, string propname, Type rettype, Type retelemtype, bool functional, PropertyBuilder property, bool add) {
			string mname = add ? "Add" : "Remove";
			MethodInfo impl = add ? anyaddvalue : anyremvalue;
			
			MethodBuilder method = t.DefineMethod(mname + propname,
				MethodAttributes.Public,
				null, new Type[] { retelemtype } );
			
			ILGenerator il = method.GetILGenerator();
			
			il.Emit(OpCodes.Ldarg_0);

			il.Emit(OpCodes.Ldstr, p.Uri);
			il.Emit(OpCodes.Newobj, newentity);

			il.Emit(OpCodes.Ldarg_1);
			
			if (forward)
				il.Emit(OpCodes.Ldc_I4_1);
			else
				il.Emit(OpCodes.Ldc_I4_0);
			
			il.Emit(OpCodes.Call, impl);
			il.Emit(OpCodes.Ret);
		}
		
		private void DefinePropertyStaticConstructor(Entity c, Entity p, TypeBuilder t, string propname) {
			bool functional = schemas.Contains(new Statement(p, RDF + "type", (Entity)"http://www.w3.org/2002/07/owl#InverseFunctionalProperty"));
				
			Type mrettype = t;
			if (!functional)
				mrettype = t.Assembly.GetType(t.FullName + "[]");
			
			MethodBuilder method = t.DefineMethod("From" + propname, MethodAttributes.Public | MethodAttributes.Static, mrettype, new Type[] { typeof(string), typeof(Store) } );
			ILGenerator il = method.GetILGenerator();
			
			il.DeclareLocal(typeof(ArrayList));
			il.DeclareLocal(typeof(int));
			il.DeclareLocal(t);

			if (!functional) {
				// Create array list in local variable 0
				il.Emit(OpCodes.Newobj, arraylistconstructor);
				il.Emit(OpCodes.Stloc_0);
			}
			
			// Push model (arg 0 of Select)
			il.Emit(OpCodes.Ldarg_1);
			
			// Predicate
			il.Emit(OpCodes.Ldstr, p.Uri);
			il.Emit(OpCodes.Newobj, newentity);
			
			// Object
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Newobj, newliteral);
			
			// Call select (pushes Entity[])
			
			il.Emit(OpCodes.Call, selectsubjects);
			
			// Loop through the entities
			
			Label loopstart = il.DefineLabel();
			Label loopend = il.DefineLabel();
			Label loopcontinue = il.DefineLabel();
			il.Emit(OpCodes.Ldc_I4_0); // initialize counter local var 1
			il.Emit(OpCodes.Stloc_1);
			
			il.MarkLabel(loopstart);
			il.Emit(OpCodes.Dup); // dup the array
			il.Emit(OpCodes.Ldlen); // push length of array
			il.Emit(OpCodes.Ldloc_1); // push counter
			il.Emit(OpCodes.Ble, loopend);
			
			il.Emit(OpCodes.Dup); // dup the array
			il.Emit(OpCodes.Ldloc_1); // push counter
			il.Emit(OpCodes.Ldelem_Ref);	// pop array, counter, push element
			
			il.Emit(OpCodes.Ldarg_1); // model
			
			ConstructorInfo ctor = GetConstructor(t);
			il.Emit(OpCodes.Newobj, ctor);			
			
			if (!functional) {
				// We need the ArrayList before this argument.
				il.Emit(OpCodes.Stloc_2);
				il.Emit(OpCodes.Ldloc_0); // push ArrayList
				il.Emit(OpCodes.Ldloc_2); // get back the object
				il.Emit(OpCodes.Call, arraylistadd); // add to ArrayList
				il.Emit(OpCodes.Pop); // pop the int32 return value of Add
			} else {
				// Store the result, clear the stack, get the result back, and return.
				il.Emit(OpCodes.Stloc_2);
				il.Emit(OpCodes.Pop); // the entities array
				il.Emit(OpCodes.Ldloc_2);
				il.Emit(OpCodes.Ret);
			}
			
			// increment counter
			il.MarkLabel(loopcontinue);
			il.Emit(OpCodes.Ldloc_1); // push counter
			il.Emit(OpCodes.Ldc_I4_1); // push 1
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stloc_1);
			
			// go to start of loop
			il.Emit(OpCodes.Br, loopstart);
			il.MarkLabel(loopend);
			il.Emit(OpCodes.Pop); // pop the Entity[] array
			
			if (!functional) {
				// Load array list and convert to array, and return.
				il.Emit(OpCodes.Ldloc_0);
				il.Emit(OpCodes.Ldtoken, t);
				il.Emit(OpCodes.Call, gettypefromhandle);
				il.Emit(OpCodes.Call, arraylisttoarray);
				il.Emit(OpCodes.Castclass, mrettype);
			} else {
				il.Emit(OpCodes.Ldnull);
			}
			
			il.Emit(OpCodes.Ret);
		}
	}
}
