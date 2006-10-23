using System;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;

namespace BDB {
	
	public enum DBFormat {
		Btree=1,
		Hash=2,
		Recno=3,
		Queue=4
	}
	
	public enum DataType {
		Any,
		UInt,
		IntArray,
		String
	}
	
	public class DatabaseException : ApplicationException {
		public DatabaseException(string message) : base(message) {
		}
	}
	
	public class BDB43 {
		const string lib = "db-4.3";
		
		Env env;
		IntPtr dbp;
		dbstruct funcs;
		bool closed = false;
		
		Serializer binfmt;
		
		public DataType
			KeyType = DataType.Any,
			ValueType = DataType.Any;
		
		public BDB43(string file, bool create, DBFormat format, bool allowDuplicates, Env environment)
			: this(file, null, create, format, allowDuplicates, environment) {
		}
		
		public BDB43(string file, string table, bool create, DBFormat format, bool allowDuplicates, Env environment) {
			this.env = environment;
			
			db_create(out dbp, environment.envptr, 0);
			funcs = (dbstruct)Marshal.PtrToStructure((IntPtr)((int)dbp+268), typeof(dbstruct));
			
			uint dbflags = DB_DIRECT_DB;
			if (allowDuplicates)
				dbflags |= DB_DUP; // DB_DUPSORT; 
			
			funcs.set_flags(dbp, dbflags);
			
			int type = (int)format;
			uint flags = DB_DIRTY_READ; // | DB_AUTO_COMMIT;
			int chmod_mode = 0;
			
			if (create)
				flags |= DB_CREATE;
			
			// if file & database are null, db is held in-memory
			// on file is taken as UTF8 (is this call right?)
			int ret = funcs.open(dbp, env.Txn, file, table, type, flags, chmod_mode);
			CheckError(ret);
			
			binfmt = new Serializer();
		}
		
		~BDB43() {
			if (!closed) 
				Close();
		}
		
		public void Close() {
			if (closed) throw new InvalidOperationException("Database is already closed.");
			funcs.close(dbp, 0);
			closed = true;
		}
		
		public void Put(object key, object value) {
			Put(key, value, 0);
		}
		
		public void Append(object key, object value) {
			Put(key, value, 2);
		}
		
		public bool PutNew(object key, object value) {
			return Put(key, value, 22);
		}

		private bool Put(object key, object value, uint flags) {
			Data dkey = new Data(), dvalue = new Data();
			try {
				dkey = Data.New(key, binfmt, KeyType);
				dvalue = Data.New(value, binfmt, ValueType);
				int ret = funcs.put(dbp, env.Txn, ref dkey, ref dvalue, flags);
				if (ret == DB_KEYEXIST) { return false; }
				CheckError(ret);
				return true;
			} finally {
				dkey.Free();
				dvalue.Free();
			}
		}
		
		public void Delete(object key) {
			Data dkey = Data.New(key, binfmt, KeyType);
			try {
				uint flags = 0;
				int ret = funcs.del(dbp, env.Txn, ref dkey, flags);
				CheckError(ret);
			} finally {
				dkey.Free();
			}
		}
		
		public object Get(object key) {
			Data dkey = new Data();
			Data dvalue = Data.New();
			try {
				dkey = Data.New(key, binfmt, KeyType);
				int ret = funcs.get(dbp, env.Txn, ref dkey, ref dvalue, 0);
				if (ret == DB_NOTFOUND || ret == DB_KEYEMPTY)
					return null;
				CheckError(ret);
				return dvalue.GetObject(binfmt, ValueType);
			} finally {
				dkey.Free();
				dvalue.Free();
			}
		}
		
		public long Truncate() {
			uint recs;
			int ret = funcs.truncate(dbp, env.Txn, out recs, 0);
			CheckError(ret);
			return recs;
		}
		
		public void Sync() {
			int ret = funcs.sync(dbp, 0);
			CheckError(ret);
		}
		
		public Cursor NewCursor() {
			IntPtr cursorp;
	    	int ret = funcs.cursor(dbp, env.Txn, out cursorp, 0);
	    	CheckError(ret);
	    	return new Cursor(this, cursorp);
		}
		
		public struct Cursor : IDisposable {
			IntPtr cursorp;
			cursorstruct funcs;
			BDB43 parent;
			
			public enum Seek {
				Current = 7,
				First = 9,
				Last = 17,
				Next = 18,
				Prev = 25,
				NextDup = 19,
				NextNoDup = 20,
				PrevNoDup = 26
			}
			
			private enum Dest {
				After = 1,
				Before = 3,
				Current = 7,
				KeyFirst = 15,
				KeyLast = 16
			}
			
			const uint DB_GET_BOTH = 10;
			const uint DB_SET = 28;
			
			internal Cursor(BDB43 parent, IntPtr cursorp) {
				this.cursorp = cursorp;
				this.parent = parent;
				funcs = (cursorstruct)Marshal.PtrToStructure((IntPtr)((int)cursorp+188), typeof(cursorstruct));
			}
			
			public long Count() {
				uint count;
				int ret = funcs.count(cursorp, out count, 0);
				CheckError(ret);
				return count;
			}
			
			public void Delete() {
				int ret = funcs.del(cursorp, 0);
				CheckError(ret);
			}
			
			public Cursor Duplicate() {
				IntPtr newp;
				int ret = funcs.dup(cursorp, out newp, 0);
				CheckError(ret);
				return new Cursor(parent, newp);
			}
			
			public object MoveTo(object key) {
				return MoveTo(key, null, false);
			}
			
			public bool MoveTo(object key, object value) {
				return MoveTo(key, value, true) != null;
			}
			
			private object MoveTo(object key, object value, bool usevalue) {
				Data dkey = new Data();
				Data dvalue = new Data();
				try {
					dkey = Data.New(key, parent.binfmt, parent.KeyType);
					if (!usevalue)
						dvalue = Data.New();
					else
						dvalue = Data.New(value, parent.binfmt, parent.ValueType);
					int ret = funcs.get(cursorp, ref dkey, ref dvalue, !usevalue ? DB_SET : DB_GET_BOTH);
					if (ret == DB_NOTFOUND || ret == DB_KEYEMPTY)
						return null;
					CheckError(ret);
					if (usevalue)
						return value;
					return dvalue.GetObject(parent.binfmt, parent.ValueType);
				} finally {
					dkey.Free();
					dvalue.Free();
				}
			}
			
			public bool Get(Seek seek, out object key, out object value) {
				Data dkey = Data.New();
				Data dvalue = Data.New();
				try {
					int ret = funcs.get(cursorp, ref dkey, ref dvalue, (uint)seek);
					if (ret == DB_NOTFOUND || ret == DB_KEYEMPTY) {
						key = null;
						value = null;
						return false;
					}
					CheckError(ret);
					key = dkey.GetObject(parent.binfmt, parent.KeyType);
					value = dvalue.GetObject(parent.binfmt, parent.ValueType);
					return true;
				} finally {
					dkey.Free();
					dvalue.Free();
				}
			}
			
			public void Append(object key, object value) {
				Put(key, value, Dest.KeyLast);
			}
			
			public void Overwrite(object value) {
				Put(null, value, Dest.Current);
			}
			
			private void Put(object key, object value, Dest putwhere) {
				Data dkey = new Data(), dvalue = new Data();
				try {
					if (key != null)
						dkey = Data.New(key, parent.binfmt, parent.KeyType);
					else
						dkey = new Data(); // for putwhere == Current
					dvalue = Data.New(value, parent.binfmt, parent.ValueType);
					int ret = funcs.put(cursorp, ref dkey, ref dvalue, (uint)putwhere);
					if (ret == DB_KEYEXIST) { return; }
					CheckError(ret);
				} finally {
					dkey.Free();
					dvalue.Free();
				}
			}
			
			public void Dispose() {
				funcs.close(cursorp);
			}
		}

		public class Env : IDisposable {
			internal IntPtr envptr;
			envstruct1 funcs1;
			envstruct2 funcs2;
			
			internal IntPtr Txn = IntPtr.Zero;
			txnstruct txnfuncs;
			
			public Env(string home) {
				int ret = db_env_create(out envptr, 0);
				CheckError(ret);
				
				funcs1 = (envstruct1)Marshal.PtrToStructure((IntPtr)((int)envptr+276), typeof(envstruct1));
				funcs2 = (envstruct2)Marshal.PtrToStructure((IntPtr)((int)envptr+712), typeof(envstruct2));
				
				funcs1.set_flags(envptr, DB_LOG_INMEMORY, 1);
				
				ret = funcs1.open(envptr, home, DB_CREATE | DB_INIT_MPOOL | DB_PRIVATE , 0); // | DB_INIT_LOCK | DB_INIT_LOG | DB_INIT_TXN
				if (ret != 0)
					funcs1.close(envptr, 0);
				CheckError(ret);
			}
			
			public void Dispose() {
				funcs1.close(envptr, 0);
				envptr = IntPtr.Zero;
			}
			
			public void BeginTransaction() {
				int ret = funcs2.txn_begin(envptr, Txn, out Txn, DB_DIRTY_READ);
				CheckError(ret);
				txnfuncs = (txnstruct)Marshal.PtrToStructure((IntPtr)((int)Txn+100), typeof(txnstruct));
			}
			
			public void Commit() {
				int ret = txnfuncs.commit(Txn, 0);
				txnfuncs.commit = null;
				Txn = IntPtr.Zero;
				CheckError(ret);
			}
			
			~Env() {
				if (envptr != IntPtr.Zero)
					funcs1.close(envptr, 0);
			}
		}
	
		static void CheckError(int error) {
			if (error == 0) return;
			throw new DatabaseException( Marshal.PtrToStringAnsi(db_strerror(error)) );
		}
		
		[DllImport(lib)] static extern int db_create(out IntPtr dbp, IntPtr dbenv, uint flags);
		[DllImport(lib)] static extern IntPtr db_strerror(int error);
		[DllImport(lib)] static extern int db_env_create(out IntPtr dbenvp, uint flags);
		
		private struct Data {
			const uint DB_DBT_MALLOC    =   0x004;
		
			public IntPtr Ptr;
			public uint Size;
#pragma warning disable 169
			uint ulen, dlen, doff;
#pragma warning restore 169
			public uint flags;
			
			static byte[] staticdata;
			
			static void staticalloc(int len) {
				if (staticdata == null) {
					staticdata = new byte[len];
				} else if (staticdata.Length < len) {
					staticdata = new byte[len];
				}
			}
			
	        static IntPtr BytesToAlloc (Array marshal, int length, int stride) {
	            IntPtr mem = Marshal.AllocHGlobal (length*stride);
	            if (mem == IntPtr.Zero)
	                throw new OutOfMemoryException ();
	            bool copied = false;
	            try {
	            	if (marshal is byte[])
	                	Marshal.Copy ((byte[])marshal, 0, mem, length);
	            	else if (marshal is int[])
	                	Marshal.Copy ((int[])marshal, 0, mem, length);
	            	else if (marshal is char[])
	                	Marshal.Copy ((char[])marshal, 0, mem, length);
	                else
	                	throw new InvalidOperationException();
	                copied = true;
	            }
	            finally {
	                if (!copied)
	                    Marshal.FreeHGlobal (mem);
	            }
	            return mem;
	        }
	        
	        public static Data New(object data, Serializer binfmt, DataType datatype) {
				Data ret = new Data();

       			if (data == null) {
					ret.Ptr = IntPtr.Zero;
					ret.Size = 0;
				} else if (datatype == DataType.UInt) {
					staticalloc(4);
					uint value = (uint)data;
					byte[] d = staticdata;
					d[0] = (byte)((value) & 0xFF);
					d[1] = (byte)((value >> 8) & 0xFF);
					d[2] = (byte)((value >> 16) & 0xFF);
					d[3] = (byte)((value >> 24) & 0xFF);
					ret.Ptr = BytesToAlloc(d, 4, 1);
					ret.Size = (uint)4;
				} else if (datatype == DataType.IntArray) {
					int[] values = (int[])data;
					ret.Size = (uint)(4*values.Length);
					ret.Ptr = BytesToAlloc(values, values.Length, 4);
				/*} else if (datatype == DataType.String && false) {
					// Somehow this is slower than the below path.
					char[] values = ((string)data).ToCharArray();
					ret.Size = (uint)(2*values.Length);
					ret.Ptr = BytesToAlloc(values, values.Length, 2);*/
       			} else {
       				MemoryStream binary = binfmt.Serialize(data);
		       		if (binary.Length > uint.MaxValue)
		       			throw new ArgumentException("data is too large");
					ret.Ptr = BytesToAlloc(binary.GetBuffer(), (int)binary.Length, 1);
					ret.Size = (uint)binary.Length;
				}
				return ret;
			}
			
			public static Data New() {
				Data ret = new Data();
				ret.flags = DB_DBT_MALLOC;
				return ret;
			}
			
			public object GetObject(Serializer binfmt, DataType datatype) {
				if (Size == 0) return null;
	        	if (datatype == DataType.UInt) {
					staticalloc((int)Size);
		        	Marshal.Copy(Ptr, staticdata, 0, (int)Size);
	        		byte[] d = staticdata;
	        		uint val = (uint)d[0] + ((uint)d[1] << 8) + ((uint)d[2] << 16) + ((uint)d[3] << 24);
					return val;
				} else if (datatype == DataType.IntArray) {
					int[] data = new int[(int)Size/4];
		        	Marshal.Copy(Ptr, data, 0, data.Length);
					return data;
				/*} else if (datatype == DataType.String && false) {
					char[] data = new char[(int)Size/2];
		        	Marshal.Copy(Ptr, data, 0, data.Length);
					return new String(data);*/
	        	} else {
					staticalloc((int)Size);
		        	Marshal.Copy(Ptr, staticdata, 0, (int)Size);
	        		return binfmt.Deserialize(staticdata);
	        	}
			}
			
			public void Free() {
				if (Ptr != IntPtr.Zero)
					Marshal.FreeHGlobal(Ptr);
			}
		}
		
#pragma warning disable 169
		const int DB_CREATE = 0x0000001;
		const int DB_DUP = 0x0000002;
		const int DB_DUPSORT = 0x0000004;
		const int DB_THREAD = 0x0000040;
		const int DB_AUTO_COMMIT = 0x01000000;
		const int DB_NOTFOUND = (-30989);/* Key/data pair not found (EOF). */
		const int DB_KEYEMPTY = (-30997);/* Key/data deleted or never created. */
		const int DB_KEYEXIST = (-30996);/* The key/data pair already exists. */
		const int DB_INIT_CDB = 0x0001000;	/* Concurrent Access Methods. */
		const int DB_INIT_LOCK = 0x0002000;	/* Initialize locking. */
		const int DB_INIT_LOG = 0x0004000;	/* Initialize logging. */
		const int DB_INIT_MPOOL = 0x0008000;	/* Initialize mpool. */
		const int DB_INIT_REP = 0x0010000;	/* Initialize replication. */
		const int DB_INIT_TXN = 0x0020000;	/* Initialize transactions. */
		const int DB_JOINENV = 0x0040000;	/* Initialize all subsystems present. */
		const int DB_LOCKDOWN = 0x0080000;	/* Lock memory into physical core. */
		const int DB_LOG_INMEMORY = 0x00020000; /* Store logs in buffers in memory. */
		const int DB_PRIVATE = 0x0100000;	/* DB_ENV is process local. */
		const int DB_TXN_NOSYNC = 0x0000100;/* Do not sync log on commit. */
		const int DB_TXN_NOT_DURABLE = 0x0000200;	/* Do not log changes. */
		const int DB_DIRTY_READ = 0x04000000; /* Dirty Read. */
		const int DB_DIRECT_DB = 0x00002000;
		
		struct dbstruct {
			IntPtr associate;
			public db_close_delegate close;
			public db_cursor_delegate cursor;
			public db_del_delegate del;
			IntPtr dump;
			IntPtr err;
			IntPtr errx;
			IntPtr dx;
			public db_get_delegate get;
			IntPtr pget;
			IntPtr get_bytesswapped;
			IntPtr get_cachesize;
			IntPtr get_dbname;
			IntPtr get_encrypt_flags;
			IntPtr get_env;
			IntPtr get_errfile;
			IntPtr get_errpfx;
			IntPtr get_flags;
			IntPtr get_lorder;
			IntPtr get_open_flags;
			IntPtr get_pagesize;
			IntPtr get_transactional;
			IntPtr get_type;
			IntPtr join;
			IntPtr key_range;
			public db_open_delegate open;
			public db_put_delegate put;
			IntPtr remove;
			IntPtr rename;
			public db_truncate_delegate truncate;
			IntPtr set_append_recno;
			IntPtr set_alloc;
			IntPtr set_cachesize;
			IntPtr set_dup_compare;
			IntPtr set_encrypt;
			IntPtr set_errcall;
			IntPtr set_errfile;
			IntPtr set_errpfx;
			IntPtr set_feedback;
			public db_set_flags_delegate set_flags;
			IntPtr set_lorder;
			IntPtr set_msgcall;
			IntPtr get_msgfile;
			IntPtr set_msgfile;
			IntPtr set_pagesize;
			IntPtr set_paniccall;
			IntPtr stat;
			IntPtr stat_print;
			public db_sync_delegate sync;
			IntPtr upgrade;
			IntPtr verify;
			IntPtr get_bt_minkey;
			IntPtr set_bt_compare;
			IntPtr set_bt_maxkey;
			IntPtr set_bt_minkey;
			IntPtr set_bt_prefix;
			IntPtr get_h_ffactor;
			IntPtr get_h_nelem;
			IntPtr set_h_ffactor;
			IntPtr set_h_hash;
			IntPtr set_h_nelem;
			IntPtr get_re_delim;
			IntPtr get_re_len;
			IntPtr get_re_pad;
			IntPtr get_re_source;
			IntPtr set_re_delim;
			IntPtr set_re_len;
			IntPtr set_re_pad;
			IntPtr set_re_source;
			IntPtr get_q_extentsize;
			IntPtr set_q_extentsize;
    	}
    	
		struct cursorstruct {
			public c_close_delegate close;
			public c_count_delegate count;
			public c_del_delegate del;
			public c_dup_delegate dup;
			public c_get_delegate get;
			IntPtr pget;
			public c_put_delegate put;
		}
		
		struct envstruct1 {
			public env_close_delegate close;
			IntPtr dbremove;
			IntPtr dbrename;
			IntPtr err;
			IntPtr errx;
			public env_open_delegate open;
			IntPtr remove;
			IntPtr stat_print;
			IntPtr fileid_reset;
			IntPtr is_bigendian;
			IntPtr lsn_reset;
			IntPtr prdbt;
			IntPtr set_alloc;
			IntPtr set_app_dispatch;
			IntPtr get_data_dirs;
			IntPtr set_data_dirs;
			IntPtr get_encrypt_flags;
			IntPtr set_encrypt;
			IntPtr set_errcall;
			IntPtr get_errfile;
			IntPtr set_errfile;
			IntPtr get_errpfx;
			IntPtr set_errpfx;
			IntPtr set_feedback;
			IntPtr get_flags;
			public env_set_flags_delegate set_flags;
		}
		
		struct envstruct2 {
			public env_tnx_begin_delegate txn_begin;
		}
		
		struct txnstruct {
			IntPtr abort;
			public txn_commit_delegate commit;
			IntPtr discard;
			IntPtr prepare;
		}
		
	#pragma warning restore 169
    	
    	delegate int db_set_flags_delegate(IntPtr db, uint flags);
		delegate int db_close_delegate(IntPtr dbp, uint flags);
		delegate int db_open_delegate(IntPtr db, IntPtr txnid, string file, string database, int type, uint flags, int mode);
		delegate int db_get_delegate(IntPtr db, IntPtr txnid, ref Data key, ref Data data, uint flags);
		delegate int db_put_delegate(IntPtr db, IntPtr txnid, ref Data key, ref Data data, uint flags);
    	delegate int db_del_delegate(IntPtr db, IntPtr txnid, ref Data key, uint flags);
    	delegate int db_truncate_delegate(IntPtr db, IntPtr txnid, out uint count, uint flags);
    	delegate int db_sync_delegate(IntPtr db, uint flags);
    	delegate int db_cursor_delegate(IntPtr db, IntPtr txnid, out IntPtr cursorp, uint flags);
    	delegate int c_close_delegate(IntPtr cursorp);
    	delegate int c_count_delegate(IntPtr cursorp, out uint count, uint flags);
    	delegate int c_del_delegate(IntPtr cursorp, uint flags);
    	delegate int c_dup_delegate(IntPtr cursorp, out IntPtr newcursorp, uint flags);
    	delegate int c_put_delegate(IntPtr cursorp, ref Data key, ref Data value, uint flags);
    	delegate int c_get_delegate(IntPtr cursorp, ref Data key, ref Data value, uint flags);
    	delegate int env_close_delegate(IntPtr envp, uint flags);
    	delegate int env_open_delegate(IntPtr envp, string home, uint flags, int mode);
    	delegate int env_tnx_begin_delegate(IntPtr envp, IntPtr parenttxn, out IntPtr txnp, uint flags);
    	delegate int env_set_flags_delegate(IntPtr envp, uint flag, int value);
    	delegate int txn_commit_delegate(IntPtr tid, uint flags);
	}
	
	class Serializer {
		BinaryFormatter binfmt;
		MemoryStream stream;
		BinaryWriter writer;
		
		public Serializer() {
			binfmt = new BinaryFormatter();
			stream = new MemoryStream();
			writer = new BinaryWriter(stream);
		}
		
		public MemoryStream Serialize(object obj) {
			// Reuse our memory stream by clearing it.
			// Not thread safe, obviously.
			stream.SetLength(0);
			
			Type type = obj.GetType();
			stream.WriteByte((byte)Type.GetTypeCode(type));
			
			switch (Type.GetTypeCode (type)) {
				case TypeCode.Boolean: writer.Write ((bool)obj); break;
				case TypeCode.Byte: writer.Write ((byte)obj); break;
				case TypeCode.Char: writer.Write ((char)obj); break;
				case TypeCode.DateTime: writer.Write (((DateTime)obj).Ticks); break;
				case TypeCode.Decimal: writer.Write ((decimal)obj); break;
				case TypeCode.Double: writer.Write ((double)obj); break;
				case TypeCode.Int16: writer.Write ((short)obj); break;
				case TypeCode.Int32: writer.Write ((int)obj); break;
				case TypeCode.Int64: writer.Write ((long)obj); break;
				case TypeCode.SByte: writer.Write ((sbyte)obj); break;
				case TypeCode.Single: writer.Write ((float)obj); break;
				case TypeCode.UInt16: writer.Write ((ushort)obj); break;
				case TypeCode.UInt32: writer.Write ((uint)obj); break;
				case TypeCode.UInt64: writer.Write ((ulong)obj); break;
				case TypeCode.String: writer.Write ((string)obj); break;
				default: // TypeCode.Object
					if (type.IsArray
						&& type.GetArrayRank() == 1
						&& Type.GetTypeCode(type.GetElementType()) != TypeCode.Object) {
						int length = ((Array)obj).Length;
						TypeCode innertype = Type.GetTypeCode(type.GetElementType());
						stream.WriteByte(0);
						stream.WriteByte((byte)innertype);
						writer.Write(length);
						for (int i = 0; i < length; i++) {
							switch (innertype) {
								case TypeCode.Boolean: writer.Write (((bool[])obj)[i]); break;
								case TypeCode.Byte: writer.Write (((byte[])obj)[i]); break;
								case TypeCode.Char: writer.Write (((char[])obj)[i]); break;
								case TypeCode.DateTime: writer.Write ((((DateTime[])obj)[i]).Ticks); break;
								case TypeCode.Decimal: writer.Write (((decimal[])obj)[i]); break;
								case TypeCode.Double: writer.Write (((double[])obj)[i]); break;
								case TypeCode.Int16: writer.Write (((short[])obj)[i]); break;
								case TypeCode.Int32: writer.Write (((int[])obj)[i]); break;
								case TypeCode.Int64: writer.Write (((long[])obj)[i]); break;
								case TypeCode.SByte: writer.Write (((sbyte[])obj)[i]); break;
								case TypeCode.Single: writer.Write (((float[])obj)[i]); break;
								case TypeCode.UInt16: writer.Write (((ushort[])obj)[i]); break;
								case TypeCode.UInt32: writer.Write (((uint[])obj)[i]); break;
								case TypeCode.UInt64: writer.Write (((ulong[])obj)[i]); break;
								case TypeCode.String: writer.Write (((string[])obj)[i]); break;
							}
						}
					} else {
						stream.WriteByte(1);
		       			binfmt.Serialize(stream, obj);
					}
					break;
			}
			return stream;
		}
		
		public object Deserialize(byte[] data) {
			MemoryStream stream = new MemoryStream(data);
			BinaryReader reader = new BinaryReader(stream);
			
			TypeCode typecode = (TypeCode)stream.ReadByte();
			
			switch (typecode) {
				case TypeCode.Boolean: return reader.ReadBoolean ();
				case TypeCode.Byte: return reader.ReadByte ();
				case TypeCode.Char: return reader.ReadChar ();
				case TypeCode.DateTime:  return new DateTime(reader.ReadInt64 ());
				case TypeCode.Decimal: return reader.ReadDecimal ();
				case TypeCode.Double: return reader.ReadDouble ();
				case TypeCode.Int16: return reader.ReadInt16 ();
				case TypeCode.Int32: return reader.ReadInt32 ();
				case TypeCode.Int64: return reader.ReadInt64 ();
				case TypeCode.SByte: return reader.ReadSByte ();
				case TypeCode.Single: return reader.ReadSingle ();
				case TypeCode.UInt16: return reader.ReadUInt16 ();
				case TypeCode.UInt32: return reader.ReadUInt32 ();
				case TypeCode.UInt64: return reader.ReadUInt64 ();
				case TypeCode.String: return reader.ReadString ();
				default: // TypeCode.Object
					byte objtype = (byte)stream.ReadByte();
					if (objtype == 0) {
						int length = reader.ReadInt32();
						TypeCode innertype = (TypeCode)reader.ReadByte();
						object obj;
						switch (typecode) {
							case TypeCode.Boolean: obj = new Boolean[length]; break;
							case TypeCode.Byte: obj = new Byte[length]; break;
							case TypeCode.Char: obj = new Char[length]; break;
							case TypeCode.DateTime:  obj = new DateTime[length]; break;
							case TypeCode.Decimal: obj = new Decimal[length]; break;
							case TypeCode.Double: obj = new Double[length]; break;
							case TypeCode.Int16: obj = new Int16[length]; break;
							case TypeCode.Int32: obj = new Int32[length]; break;
							case TypeCode.Int64: obj = new Int64[length]; break;
							case TypeCode.SByte: obj = new SByte[length]; break;
							case TypeCode.Single: obj = new Single[length]; break;
							case TypeCode.UInt16: obj = new UInt16[length]; break;
							case TypeCode.UInt32: obj = new UInt32[length]; break;
							case TypeCode.UInt64: obj = new UInt64[length]; break;
							case TypeCode.String: obj = new String[length]; break;
							default: throw new InvalidOperationException();
						}
						for (int i = 0; i < length; i++) {
							switch (innertype) {
								case TypeCode.Boolean: (((bool[])obj)[i]) = reader.ReadBoolean(); break;
								case TypeCode.Byte: (((byte[])obj)[i]) = reader.ReadByte(); break;
								case TypeCode.Char: (((char[])obj)[i]) = reader.ReadChar(); break;
								case TypeCode.DateTime: (((DateTime[])obj)[i]) = new DateTime(reader.ReadInt32()); break;
								case TypeCode.Decimal: (((decimal[])obj)[i]) = reader.ReadDecimal(); break;
								case TypeCode.Double: (((double[])obj)[i]) = reader.ReadDouble(); break;
								case TypeCode.Int16: (((short[])obj)[i]) = reader.ReadInt16(); break;
								case TypeCode.Int32: (((int[])obj)[i]) = reader.ReadInt32(); break;
								case TypeCode.Int64: (((long[])obj)[i]) = reader.ReadInt64(); break;
								case TypeCode.SByte: (((sbyte[])obj)[i]) = reader.ReadSByte(); break;
								case TypeCode.Single: (((float[])obj)[i]) = reader.ReadSingle(); break;
								case TypeCode.UInt16: (((ushort[])obj)[i]) = reader.ReadUInt16(); break;
								case TypeCode.UInt32: (((uint[])obj)[i]) = reader.ReadUInt32(); break;
								case TypeCode.UInt64: (((ulong[])obj)[i]) = reader.ReadUInt64(); break;
								case TypeCode.String: (((string[])obj)[i]) = reader.ReadString(); break;
							}
						}
						return obj;
					} else if (objtype == 1) {
						return binfmt.Deserialize(stream);
					} else {
						throw new InvalidOperationException();
					}
			}
		}
	}
	
}

