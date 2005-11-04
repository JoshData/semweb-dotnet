using System;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using Mono.Unix;

namespace BDB {
	
	public enum DBFormat {
		Btree=1,
		Hash=2,
		Recno=3,
		Queue=4
	}
	
	public class DatabaseException : ApplicationException {
		public DatabaseException(string message) : base(message) {
		}
	}
	
	public class BDB43 {
		const string lib = "db-4.3";
		
		const int DB_CREATE = 0x0000001;
		//const int DB_DUP = 0x0000002;
		const int DB_DUPSORT = 0x0000004;
		const int DB_THREAD = 0x0000040;
		const int DB_NOTFOUND = (-30989);/* Key/data pair not found (EOF). */
		const int DB_KEYEMPTY = (-30997);/* Key/data deleted or never created. */
		const int DB_KEYEXIST = (-30996);/* The key/data pair already exists. */
		
		IntPtr dbp;
		dbstruct funcs;
		bool closed = false;
		
		BinaryFormatter binfmt;
		
		public BDB43(string file, bool create, DBFormat format, bool allowDuplicates)
			: this(file, null, create, format, allowDuplicates) {
		}
		
		public BDB43(string file, string table, bool create, DBFormat format, bool allowDuplicates) {
			db_create(out dbp, IntPtr.Zero, 0);
			funcs = (dbstruct)Marshal.PtrToStructure((IntPtr)((int)dbp+268), typeof(dbstruct));
			
			uint dbflags = 0;
			if (allowDuplicates)
				dbflags |= DB_DUPSORT; 
			
			funcs.set_flags(dbp, dbflags);
			
			int type = (int)format;
			uint flags = 0; //DB_THREAD;
			int chmod_mode = 0;
			
			if (create)
				flags |= DB_CREATE;
			
			// if file & database are null, db is held in-memory
			// on file is taken as UTF8 (is this call right?)
			int ret = funcs.open(dbp, IntPtr.Zero, file, table, type, flags, chmod_mode);
			CheckError(ret);
			
			binfmt = new BinaryFormatter();
			binfmt.AssemblyFormat = FormatterAssemblyStyle.Simple;
			binfmt.TypeFormat = FormatterTypeStyle.TypesWhenNeeded;
			
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
				dkey = Data.New(key, binfmt);
				dvalue = Data.New(value, binfmt);
				int ret = funcs.put(dbp, IntPtr.Zero, ref dkey, ref dvalue, flags);
				if (ret == DB_KEYEXIST) { return true; }
				CheckError(ret);
				return false;
			} finally {
				dkey.Free();
				dvalue.Free();
			}
		}
		
		public void Delete(object key) {
			Data dkey = Data.New(key, binfmt);
			try {
				uint flags = 0;
				int ret = funcs.del(dbp, IntPtr.Zero, ref dkey, flags);
				CheckError(ret);
			} finally {
				dkey.Free();
			}
		}
		
		public object Get(object key) {
			Data dkey = new Data();
			Data dvalue = Data.New();
			try {
				dkey = Data.New(key, binfmt);
				uint flags = 0;
				int ret = funcs.get(dbp, IntPtr.Zero, ref dkey, ref dvalue, flags);
				if (ret == DB_NOTFOUND || ret == DB_KEYEMPTY)
					return null;
				CheckError(ret);
				return dvalue.GetObject(binfmt);
			} finally {
				dkey.Free();
				dvalue.Free();
			}
		}
		
		public long Truncate() {
			uint recs;
			int ret = funcs.truncate(dbp, IntPtr.Zero, out recs, 0);
			CheckError(ret);
			return recs;
		}
		
		public void Sync() {
			int ret = funcs.sync(dbp, 0);
			CheckError(ret);
		}
		
		public Cursor NewCursor() {
			IntPtr cursorp;
	    	int ret = funcs.cursor(dbp, IntPtr.Zero, out cursorp, 0);
	    	CheckError(ret);
	    	return new Cursor(cursorp, binfmt);
		}
		
		public struct Cursor : IDisposable {
			IntPtr cursorp;
			cursorstruct funcs;
			BinaryFormatter binfmt;
			
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
			
			internal Cursor(IntPtr cursorp, BinaryFormatter binfmt) {
				this.cursorp = cursorp;
				this.binfmt = binfmt;
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
				return new Cursor(newp, binfmt);
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
					dkey = Data.New(key, binfmt);
					if (!usevalue)
						dvalue = Data.New();
					else
						dvalue = Data.New(value, binfmt);
					int ret = funcs.get(cursorp, ref dkey, ref dvalue, !usevalue ? DB_SET : DB_GET_BOTH);
					if (ret == DB_NOTFOUND || ret == DB_KEYEMPTY)
						return null;
					CheckError(ret);
					if (usevalue)
						return value;
					return dvalue.GetObject(binfmt);
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
					key = dkey.GetObject(binfmt);
					value = dvalue.GetObject(binfmt);
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
						dkey = Data.New(key, binfmt);
					else
						dkey = new Data(); // for putwhere == Current
					dvalue = Data.New(value, binfmt);
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

		/*static IntPtr CreateEnvironment() {
			IntPtr env;
			int ret = db_env_create(out env, DB_TREAD);
			CheckError(ret);
			return env;
		}*/
		
		static void CheckError(int error) {
			if (error == 0) return;
			throw new DatabaseException( Marshal.PtrToStringAnsi(db_strerror(error)) );
		}
		
		[DllImport(lib)] static extern int db_create(out IntPtr dbp, IntPtr dbenv, uint flags);
		[DllImport(lib)] static extern IntPtr db_strerror(int error);
		//[DllImport(lib)] static extern int db_env_create(out IntPtr dbenvp, uint flags);
		
		private struct Data {
			const uint DB_DBT_MALLOC    =   0x004;
		
			public IntPtr Ptr;
			public uint Size;
#pragma warning disable 169
			uint ulen, dlen, doff;
#pragma warning restore 169
			public uint flags;
			
			// From Mono.Unix.UnixMarshal
	        /*static IntPtr StringToAlloc (string s, System.Text.Encoding e, out uint bytelength) {
	            byte[] marshal = new byte [e.GetByteCount (s) + 1];
	            if (e.GetBytes (s, 0, s.Length, marshal, 0) != (marshal.Length-1))
	                throw new NotSupportedException ("e.GetBytes() doesn't equal e.GetByteCount()!");
	            marshal [marshal.Length-1] = 0;
	            bytelength = (uint)marshal.Length;
	            return BytesToAlloc(marshal, marshal.Length);
	        }*/
	        
	        static IntPtr BytesToAlloc (byte[] marshal, int length) {
	            IntPtr mem = UnixMarshal.Alloc (length);
	            if (mem == IntPtr.Zero)
	                throw new OutOfMemoryException ();
	            bool copied = false;
	            try {
	                Marshal.Copy (marshal, 0, mem, length);
	                copied = true;
	            }
	            finally {
	                if (!copied)
	                    UnixMarshal.Free (mem);
	            }
	            return mem;
	        }
	        
       		/*public static Data New(string data) {
				Data ret = new Data();
				ret.Ptr = StringToAlloc(data, System.Text.Encoding.UTF8, out ret.Size);
				ret.ulen = 0;
				ret.dlen = 0;
				ret.doff = 0;
				ret.flags = 0;
				return ret;
			}*/
			
			static MemoryStream binary;
       		static BinaryWriter w;
			
       		public static Data New(object data, BinaryFormatter binfmt) {
       			if (data == null) {
					Data ret = new Data();
					ret.Ptr = IntPtr.Zero;
					ret.Size = 0;
					return ret;
       			} else {
       				if (binary == null) {
       					binary = new MemoryStream();
       					w = new BinaryWriter(binary);
       				}
       				binary.SetLength(0);
       			
	       			if (data is string) {
	       				binary.WriteByte(1);
	       				w.Write((string)data);
	       			} else if (data is int) {
	       				binary.WriteByte(2);
	       				w.Write((int)data);
	       			} else if (data is int[]) {
	       				binary.WriteByte(3);
	       				int[] d = (int[])data;
	       				w.Write(d.Length);
	       				foreach (int di in d)
	       					w.Write(di);
	       			} else {
	       				binary.WriteByte(0);
		       			binfmt.Serialize(binary, data);
		       			if (binary.Length > uint.MaxValue)
		       				throw new ArgumentException("data is too large");
	       			}
					Data ret = new Data();
					ret.Ptr = BytesToAlloc(binary.GetBuffer(), (int)binary.Length);
					ret.Size = (uint)binary.Length;
					return ret;
				}
			}
			
			public static Data New() {
				Data ret = new Data();
				ret.flags = DB_DBT_MALLOC;
				return ret;
			}
			
			public object GetObject(BinaryFormatter binfmt) {
				if (Size == 0) return null;
				byte[] bytedata = new byte[Size];
	        	Marshal.Copy(Ptr, bytedata, 0, (int)Size);
	        	MemoryStream binary = new MemoryStream(bytedata);
	        	int mode = binary.ReadByte();
        		BinaryReader r = new BinaryReader(binary);
	        	switch (mode) {
	        		case 1:
	        		return r.ReadString();
	        		
	        		case 2:
	        		return r.ReadInt32();
	        	
	        		case 3:
	        		{
	        			int len = r.ReadInt32();
	        			int[] ret = new int[len];
	        			for (int i = 0; i < len; i++)
	        				ret[i] = r.ReadInt32();
	        			return ret;
	        		}
	        		
	        		case 0:
	        		return binfmt.Deserialize(binary);
	        		
	        		default:
	        		throw new InvalidOperationException();
	        	}
			}
			
			public void Free() {
				if (Ptr != IntPtr.Zero)
					UnixMarshal.Free(Ptr);
			}
		}
		
#pragma warning disable 169
		[StructLayout(LayoutKind.Sequential)]
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
	}
	
}

