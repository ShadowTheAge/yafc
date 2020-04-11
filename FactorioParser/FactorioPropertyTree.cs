using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Parser
{
    public static class FactorioPropertyTree
    {
        private static int ReadSpaceOptimizedUint(BinaryReader reader)
        {
            var b = reader.ReadByte();
            if (b < 255)
                return b;
            return reader.ReadInt32();
        }

        private static string ReadString(BinaryReader reader)
        {
            if (reader.ReadBoolean())
                return "";
            var len = ReadSpaceOptimizedUint(reader);
            var bytes = reader.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        public static object ReadModSettings(BinaryReader reader)
        {
            reader.ReadInt64();
            reader.ReadBoolean();
            return ReadAny(reader);
        }

        private static object ReadAny(BinaryReader reader)
        {
            var type = reader.ReadByte();
            reader.ReadByte();
            switch (type)
            {
                case 0:
                    return null;
                case 1:
                    return reader.ReadBoolean();
                case 2:
                    return reader.ReadDouble();
                case 3:
                    return ReadString(reader);
                case 4:
                    var count = reader.ReadInt32();
                    var arr = new object[count];
                    for (var i = 0; i < count; i++)
                    {
                        ReadString(reader);
                        arr[i] = ReadAny(reader);
                    }

                    return arr;
                case 5:
                    count = reader.ReadInt32();
                    var dict = new Dictionary<string, object>(count);
                    for (var i = 0; i < count; i++)
                        dict[ReadString(reader)] = ReadAny(reader);
                    return dict;
                default:
                    throw new NotSupportedException("Unknown type");
            }
        }
    }
}