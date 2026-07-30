using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// A dependency-free <see cref="IBodyCodec"/> that walks a message's public
    /// instance fields with reflection and writes them in a stable order. It is the
    /// default used by the engine-agnostic core and its csc/mono tests where the
    /// MessagePack assembly's netstandard/System.Memory facades are unavailable.
    /// Production Unity builds swap in the MessagePack codec instead. Supports the
    /// common scalar/string/byte[] field types game messages use.
    /// </summary>
    public sealed class ReflectionBodyCodec : IBodyCodec
    {
        readonly Dictionary<Type, FieldInfo[]> _layouts = new Dictionary<Type, FieldInfo[]>();

        FieldInfo[] LayoutOf(Type type)
        {
            if (_layouts.TryGetValue(type, out var cached))
                return cached;

            var fields = new List<FieldInfo>(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
            // Stable, runtime-independent ordering so pack/unpack agree.
            fields.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            var layout = fields.ToArray();
            _layouts[type] = layout;
            return layout;
        }

        public byte[] Pack(Message message)
        {
            using var buffer = new MemoryStream();
            using var w = new BinaryWriter(buffer);
            foreach (var field in LayoutOf(message.GetType()))
                WriteValue(w, field.FieldType, field.GetValue(message));
            w.Flush();
            return buffer.ToArray();
        }

        public Message Unpack(Type type, ArraySegment<byte> body)
        {
            var message = (Message)Activator.CreateInstance(type);
            using var buffer = new MemoryStream(body.Array, body.Offset, body.Count, false);
            using var r = new BinaryReader(buffer);
            foreach (var field in LayoutOf(type))
                field.SetValue(message, ReadValue(r, field.FieldType));
            return message;
        }

        static void WriteValue(BinaryWriter w, Type t, object v)
        {
            if (t.IsEnum) t = Enum.GetUnderlyingType(t);

            if (t == typeof(int)) w.Write((int)v);
            else if (t == typeof(uint)) w.Write((uint)v);
            else if (t == typeof(long)) w.Write((long)v);
            else if (t == typeof(ulong)) w.Write((ulong)v);
            else if (t == typeof(short)) w.Write((short)v);
            else if (t == typeof(ushort)) w.Write((ushort)v);
            else if (t == typeof(byte)) w.Write((byte)v);
            else if (t == typeof(sbyte)) w.Write((sbyte)v);
            else if (t == typeof(bool)) w.Write((bool)v);
            else if (t == typeof(float)) w.Write((float)v);
            else if (t == typeof(double)) w.Write((double)v);
            else if (t == typeof(string)) WriteString(w, (string)v);
            else if (t == typeof(byte[])) WriteBlob(w, (byte[])v);
            else throw new NetworkFault("ReflectionBodyCodec cannot serialize field type " + t.FullName);
        }

        static object ReadValue(BinaryReader r, Type t)
        {
            Type underlying = t.IsEnum ? Enum.GetUnderlyingType(t) : t;
            object raw;

            if (underlying == typeof(int)) raw = r.ReadInt32();
            else if (underlying == typeof(uint)) raw = r.ReadUInt32();
            else if (underlying == typeof(long)) raw = r.ReadInt64();
            else if (underlying == typeof(ulong)) raw = r.ReadUInt64();
            else if (underlying == typeof(short)) raw = r.ReadInt16();
            else if (underlying == typeof(ushort)) raw = r.ReadUInt16();
            else if (underlying == typeof(byte)) raw = r.ReadByte();
            else if (underlying == typeof(sbyte)) raw = r.ReadSByte();
            else if (underlying == typeof(bool)) raw = r.ReadBoolean();
            else if (underlying == typeof(float)) raw = r.ReadSingle();
            else if (underlying == typeof(double)) raw = r.ReadDouble();
            else if (underlying == typeof(string)) return ReadString(r);
            else if (underlying == typeof(byte[])) return ReadBlob(r);
            else throw new NetworkFault("ReflectionBodyCodec cannot deserialize field type " + t.FullName);

            return t.IsEnum ? Enum.ToObject(t, raw) : raw;
        }

        static void WriteString(BinaryWriter w, string s)
        {
            if (s == null) { w.Write(-1); return; }
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        static string ReadString(BinaryReader r)
        {
            int len = r.ReadInt32();
            if (len < 0) return null;
            return System.Text.Encoding.UTF8.GetString(r.ReadBytes(len));
        }

        static void WriteBlob(BinaryWriter w, byte[] b)
        {
            if (b == null) { w.Write(-1); return; }
            w.Write(b.Length);
            w.Write(b);
        }

        static byte[] ReadBlob(BinaryReader r)
        {
            int len = r.ReadInt32();
            if (len < 0) return null;
            return r.ReadBytes(len);
        }
    }
}
