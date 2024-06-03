using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Logix
{
    public static class StructPack
    {
        public static byte[] Pack<T>(T structure, int padBytes = 0)
        {
            byte[] buffer = new byte[Marshal.SizeOf<T>() + padBytes];
            GCHandle hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr<T>(structure, hBuffer.AddrOfPinnedObject(), false);
            }
            finally
            {
                hBuffer.Free();
            }
            return buffer;
        }

        public static T Unpack<T>(byte[] buffer, int offset = 0)
        {
            if (buffer.Length < Marshal.SizeOf<T>())
            {
                throw new ArgumentException("Insufficient length", "buffer");
            }

            GCHandle hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(hBuffer.AddrOfPinnedObject() + offset);
            }
            finally
            {
                hBuffer.Free();
            }
        }

        public static void ToStream<T>(T structure, Stream stream)
        {
            byte[] buffer = Pack<T>(structure);
            stream.Write(buffer, 0, buffer.Length);
        }

        public static T FromStream<T>(Stream stream)
        {
            byte[] buffer = new byte[Marshal.SizeOf<T>()];
            int index = 0;
            while (index < buffer.Length)
            {
                int bytes = stream.Read(buffer, 0, buffer.Length);
                index += bytes;
            }
            return Unpack<T>(buffer);
        }
    }
}
