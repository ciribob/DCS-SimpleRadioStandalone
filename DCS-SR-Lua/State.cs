using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Ciribob.DCS.SimpleRadio.Standalone.Lua
{
    // Thin lua wrapper over the Native API, to handle the .NET/raw conversions.
    internal struct State
    {
        public IntPtr Handle { get; }

        public sealed class TableBuilder : IDisposable
        {
            State State { get; }
            public TableBuilder(State state, int arrayCount, int kvCount)
            {
                State = state;
                Native.lua_createtable(State.Handle, arrayCount, kvCount);
            }

            public TableBuilder AddField(string name, string value)
            {
                State.Push(value);
                State.SetField(-2, name);
                return this;
            }

            public TableBuilder AddField(string name, bool value)
            {
                State.Push(value);
                State.SetField(-2, name);
                return this;
            }

            public TableBuilder AddField(string name, int value)
            {
                State.Push(value);
                State.SetField(-2, name);
                return this;
            }

            public TableBuilder AddField(string name, double value)
            {
                State.Push(value);
                State.SetField(-2, name);
                return this;
            }

            public void Dispose()
            {
                // No op, just used for scoping.
            }
        }

        public static Native.Register CreateRegister(string name, Native.LuaFunction func)
        {
            return new()
            {
                name = name,
                function = func
            };
        }

        public State(IntPtr state)
        {
            Handle = state;
        }

        public void Register(string name, IEnumerable<Native.Register> registrar)
        {
            Native.luaL_register(Handle, name, registrar.ToArray());
        }

        public string CheckString(int arg)
        {
            
            UIntPtr len;
            var bytes = Native.luaL_checklstring(Handle, arg, out len);
            if (len == 0)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUTF8(bytes, (int)len);
        }

        public int CheckInteger(int arg)
        {
            return (int)Native.luaL_checkinteger(Handle, arg);
        }

        public double CheckNumber(int arg)
        {
            return Native.luaL_checknumber(Handle, arg);
        }

        public void Push(string value)
        {
            if (value != null)
            {
                Native.lua_pushlstring(Handle, value, (UIntPtr)value.Length);
            }
            else
            {
                Native.lua_pushnil(Handle);
            }
        }

        public void Push(bool value)
        {
            Native.lua_pushboolean(Handle, value ? 1 : 0);
        }

        public void Push(int value)
        {
            Native.lua_pushinteger(Handle, value);
        }

        public void Push(double value)
        {
            Native.lua_pushnumber(Handle, value);
        }

        public bool IsTable(int index)
        {
            return Native.lua_istable(Handle, index);
        }

        public TableBuilder CreateTable(int arrayCount, int kvCount)
        {
            return new TableBuilder(this, arrayCount, kvCount);
        }

        public void GetField(int index, string field)
        {
            Native.lua_getfield(Handle, index, field);
        }

        public void SetField(int index, string field)
        {
            Native.lua_setfield(Handle, index, field);
        }

        public int TypError(int index, string expected)
        {
            return Native.luaL_typerror(Handle, index, expected);
        }
    }
}
