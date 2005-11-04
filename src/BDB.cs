using System;
using System.IO;
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
		const int DB_THREAD = 0x0000040;
		const int DB_NOTFOUND = (-30989);/* Key/data pair not found (EOF). */
		const int DB_KEYEMPTY = (-30997);/* Key/data deleted or never created. */
		const int DB_KEYEXIST = (-30996);/* The key/data pair already exists. */
		
		IntPtr dbp;
		dbstruct funcs;
		bool closed = false;
		
		BinaryFormatter binfmt = new BinaryFormatter();
		
		public BDB43(string file, string table, bool create, DBFormat format) {
			db_create(out dbp, IntPtr.Zero, 0);
			funcs = (dbstruct)Marshal.PtrToStructure((IntPtr)((int)dbp+268), typeof(dbstruct));
			
			int type = (int)format;
			uint flags = DB_THREAD;
			int chmod_mode = 0;
			
			if (create)
				flags |= DB_CREATE;
			
			// if file & database are null, db is held in-memory
			// on file is taken as UTF8 (is this call right?)
			int ret = funcs.open(dbp, IntPtr.Zero, file, table, type, flags, chmod_mode);
			CheckError(ret);
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
		
		void CheckError(int error) {
			if (error == 0) return;
			throw new DatabaseException( Marshal.PtrToStringAnsi(db_strerror(error)) );
		}
		
		[DllImport(lib)] static extern int db_create(out IntPtr dbp, IntPtr dbenv, uint flags);
		[DllImport(lib)] static extern IntPtr db_strerror(int error);
		
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
			
       		public static Data New(object data, BinaryFormatter binfmt) {
       			if (data == null) {
					Data ret = new Data();
					ret.Ptr = IntPtr.Zero;
					ret.Size = 0;
					return ret;
       			} else {
	       			MemoryStream binary = new MemoryStream();
	       			binfmt.Serialize(binary, data);
	       			if (binary.Length > uint.MaxValue)
	       				throw new ArgumentException("data is too large");
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
				byte[] ret = new byte[Size];
	        	Marshal.Copy(Ptr, ret, 0, (int)Size);
	        	MemoryStream binary = new MemoryStream(ret);
	        	return binfmt.Deserialize(binary);
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
			IntPtr set_flags;
			IntPtr set_lorder;
			IntPtr set_msgcall;
			IntPtr get_msgfile;
			IntPtr set_msgfile;
			IntPtr set_pagesize;
			IntPtr set_paniccall;
			IntPtr stat;
			IntPtr stat_print;
			public db_sync_delegate sync;
			IntPtr upgrade) __P((DB *, const char *, u_int32_t));
			IntPtr verify) __P((DB *,
	    const char *, const char *, FILE *, u_int32_t));

			IntPtr get_bt_minkey) __P((DB *, u_int32_t *));
			IntPtr set_bt_compare) __P((DB *,
	    int (*)(DB *, const DBT *, const DBT *)));
			IntPtr set_bt_maxkey) __P((DB *, u_int32_t));
			IntPtr set_bt_minkey) __P((DB *, u_int32_t));
			IntPtr set_bt_prefix) __P((DB *,
	    size_t (*)(DB *, const DBT *, const DBT *)));

			IntPtr get_h_ffactor) __P((DB *, u_int32_t *));
			IntPtr get_h_nelem) __P((DB *, u_int32_t *));
			IntPtr set_h_ffactor) __P((DB *, u_int32_t));
			IntPtr set_h_hash) __P((DB *,
	    u_int32_t (*)(DB *, const void *, u_int32_t)));
			IntPtr set_h_nelem) __P((DB *, u_int32_t));

			IntPtr get_re_delim) __P((DB *, int *));
			IntPtr get_re_len) __P((DB *, u_int32_t *));
			IntPtr get_re_pad) __P((DB *, int *));
			IntPtr get_re_source) __P((DB *, const char **));
			IntPtr set_re_delim) __P((DB *, int));
			IntPtr set_re_len) __P((DB *, u_int32_t));
			IntPtr set_re_pad) __P((DB *, int));
			IntPtr set_re_source) __P((DB *, const char *));

			IntPtr get_q_extentsize) __P((DB *, u_int32_t *));
			IntPtr set_q_extentsize) __P((DB *, u_int32_t));
	
			IntPtr 
			IntPtr db_am_remove) __P((DB *, DB_TXN *, const char *, const char *));
			IntPtr db_am_rename) __P((DB *, DB_TXN *,
	    const char *, const char *, const char *));
    	}
#pragma warning restore 169
    	
		delegate int db_close_delegate(IntPtr dbp, uint flags);
		delegate int db_open_delegate(IntPtr db, IntPtr txnid, string file, string database, int type, uint flags, int mode);
		delegate int db_get_delegate(IntPtr db, IntPtr txnid, ref Data key, ref Data data, uint flags);
		delegate int db_put_delegate(IntPtr db, IntPtr txnid, ref Data key, ref Data data, uint flags);
    	delegate int db_del_delegate(IntPtr db, IntPtr txnid, ref Data key, uint flags);
    	delegate int db_truncate_delegate(IntPtr db, IntPtr txnid, out uint count, uint flags);
    	delegate int db_sync_delegate(IntPtr db, uint flags);
    	delegate int db_cursor_delegate(IntPtr db, IntPtr txnid, out IntPtr cursorp, uint flags);
	}
	
}

