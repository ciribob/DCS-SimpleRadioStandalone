using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Ciribob.DCS.SimpleRadio.Standalone.Lua
{
    // https://www.lua.org/manual/5.1/#index
    using lua_State = IntPtr;

    // While tempting, should NOT use the Charset.Ansi.
    // lua 'strings' are fairly loose, and the game looks to be using mainly UTF8.
    // We need to go through encoding to do the conversion to/from UTF16 instead.
    internal partial class Native
    {
        internal delegate int LuaFunction(lua_State luaState);

        [NativeMarshalling(typeof(RegisterMarshaller))]
        internal struct Register
        {
            /// <summary>
            /// Function name
            /// </summary>
            public string name;
            /// <summary>
            /// Function delegate
            /// </summary>
            public LuaFunction function;
        }

        [CustomMarshaller(typeof(Register), MarshalMode.ManagedToUnmanagedIn, typeof(RegisterMarshaller))]
        [CustomMarshaller(typeof(Register), MarshalMode.ElementIn, typeof(RegisterMarshaller))]
        internal static unsafe class RegisterMarshaller
        {
            // Unmanaged representation of ErrorData.
            // Should mimic the unmanaged error_data type at a binary level.
            internal struct RegisterUnmanaged
            {
                public byte* Name;    // The C++ bool is defined as a single byte.
                public IntPtr Function;
            }

            public static RegisterUnmanaged ConvertToUnmanaged(Register managed)
            {
                return new()
                {
                    Name = Utf8StringMarshaller.ConvertToUnmanaged(managed.name),
                    Function = managed.function != null? Marshal.GetFunctionPointerForDelegate(managed.function) : IntPtr.Zero
                };
            }

            public static void Free(RegisterUnmanaged unmanaged)
            {
                Utf8StringMarshaller.Free(unmanaged.Name);
            }

            public static Register ConvertToManaged(RegisterUnmanaged unmanaged) => throw new NotImplementedException();
        }

        internal enum Types
        {
            LUA_TNIL =		        0,
            LUA_TBOOLEAN =		    1,
            LUA_TLIGHTUSERDATA =    2,
            LUA_TNUMBER =		    3,
            LUA_TSTRING =		    4,
            LUA_TTABLE =		    5,
            LUA_TFUNCTION =		    6,
            LUA_TUSERDATA =		    7,
            LUA_TTHREAD =		    8
        }

        [LibraryImport("lua", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial void luaL_register(lua_State L, string libname, [In] Register[] l);

        [LibraryImport("lua", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial void lua_pushlstring(lua_State L, string buffer, UIntPtr len);

        [LibraryImport("lua", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial IntPtr luaL_checklstring(lua_State L, int numArg, out UIntPtr len);

        [LibraryImport("lua")]
        internal static partial IntPtr luaL_checkinteger(lua_State L, int narg);

        [LibraryImport("lua")]
        internal static partial double luaL_checknumber(lua_State L, int narg);

        [LibraryImport("lua")]
        internal static partial void lua_pushboolean(lua_State L, int b);
        [LibraryImport("lua")]
        internal static partial void lua_pushnil(lua_State L);

        [LibraryImport("lua")]
        internal static partial int lua_error(lua_State L);

        [LibraryImport("lua")]
        internal static partial int lua_pushnumber(lua_State L, double d);
        [LibraryImport("lua")]
        internal static partial int lua_pushinteger(lua_State L, IntPtr d);

        [LibraryImport("lua", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int luaL_typerror(lua_State L, int narg, string tname);

        [LibraryImport("lua", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial void lua_getfield(lua_State L, int index, string k);

        [LibraryImport("lua")]
        internal static partial void lua_createtable(lua_State L, int narr, int nrec);

        [LibraryImport("lua")]
        internal static partial void lua_settable(lua_State L, int index);

        [LibraryImport("lua", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial void lua_setfield(lua_State L, int index, string k);

        [LibraryImport("lua")]
        internal static partial Types lua_type(lua_State L, int idx);

        #region Macros
        internal static bool lua_istable(lua_State L, int idx)
        {
            return lua_type(L,  idx) ==  Types.LUA_TTABLE;
        }
        #endregion Macros
    }
}
