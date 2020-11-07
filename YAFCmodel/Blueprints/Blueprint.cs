using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using YAFC.Model;

namespace YAFC.Blueprints
{
    [Serializable]
    public class BlueprintString
    {
        public Blueprint blueprint { get; set; } = new Blueprint();
        private static readonly byte[] header = {0x78, 0xDA};

        public string ToBpString()
        {
            using var sourceBytes = new MemoryStream();
            using (var writer = new Utf8JsonWriter(sourceBytes))
            {
                SerializationMap<BlueprintString>.SerializeToJson(this, writer);
            }
            using var memory = new MemoryStream();
            memory.Write(header);
            sourceBytes.Position = 0;
            using (var compress = new DeflateStream(memory, CompressionLevel.Optimal, true))
                sourceBytes.CopyTo(compress);
            memory.Write(GetChecksum(sourceBytes.GetBuffer(), sourceBytes.Length));
            return "0" + Convert.ToBase64String(memory.ToArray());
        }

        private byte[] GetChecksum(byte[] buffer, long length)
        {
            int a = 1, b = 0;
            for (var counter = 0; counter < length; ++counter)
            {
                a = (a + (buffer[counter])) % 65521;
                b = (b + a) % 65521;
            }
            var checksum = (b * 65536) + a;
            var intBytes = BitConverter.GetBytes(checksum);
            Array.Reverse(intBytes);
            return intBytes;
        }
        
        public string ToJson()
        {
            using var memory = new MemoryStream();
            using (var writer = new Utf8JsonWriter(memory))
                SerializationMap<BlueprintString>.SerializeToJson(this, writer);
            memory.Position = 0;
            using (var reader = new StreamReader(memory))
                return reader.ReadToEnd();
        }
    }
    
    [Serializable]
    public class Blueprint
    {
        public const int VERSION = 0x01000000;

        public string item { get; set; } = "blueprint";
        public string label { get; set; }
        public List<BlueprintEntity> entities { get; } = new List<BlueprintEntity>();
        public List<BlueprintIcon> icons { get; } = new List<BlueprintIcon>();
        public int version { get; set; } = VERSION;
    }

    [Serializable]
    public class BlueprintIcon
    {   
        public int index { get; set; }
        public BlueprintSignal signal { get; set; } = new BlueprintSignal();
    }

    [Serializable]
    public class BlueprintSignal
    {
        public string name { get; set; }
        public string type { get; set; }

        public void Set(Goods goods)
        {
            if (goods is Special sp)
            {
                type = "virtual";
                name = sp.virtualSignal;
            }
            else
            {
                name = goods.name;
                type = goods is Fluid ? "fluid" : "item";
            }
        }
    }

    [Serializable]
    public class BlueprintEntity
    {
        public int entity_number { get; set; }
        public string name { get; set; }
        public BlueprintPosition position { get; set; } = new BlueprintPosition();
        public int direction { get; set; }
        public string recipe { get; set; }
        public BlueprintControlBehaviour control_behavior { get; set; }
        public BlueprintConnection connections { get; set; }

        public void Connect(BlueprintEntity other, bool red = true, bool secondPort = false, bool targetSecond = false)
        {
            ConnectSingle(other, red, secondPort, targetSecond);
            other.ConnectSingle(this, red, targetSecond, secondPort);
        }

        private void ConnectSingle(BlueprintEntity other, bool red = true, bool secondPort = false, bool targetSecond = false)
        {
            connections ??= new BlueprintConnection();
            BlueprintConnectionPoint port;
            if (secondPort)
                port = connections.p2 ?? (connections.p2 = new BlueprintConnectionPoint());
            else port = connections.p1 ?? (connections.p1 = new BlueprintConnectionPoint());
            var list = red ? port.red : port.green;
            list.Add(new BlueprintConnectionData {entity_id = other.entity_number, circuit_id = targetSecond ? 2 : 1});
        }
    }

    [Serializable]
    public class BlueprintConnection
    {
        [SerializationParameters(name = "1")] public BlueprintConnectionPoint p1 { get; set; }
        [SerializationParameters(name = "2")] public BlueprintConnectionPoint p2 { get; set; }
    }

    [Serializable]
    public class BlueprintConnectionPoint
    {
        public List<BlueprintConnectionData> red { get; } = new List<BlueprintConnectionData>();
        public List<BlueprintConnectionData> green { get; } = new List<BlueprintConnectionData>();
    }

    [Serializable]
    public class BlueprintConnectionData
    {
        public int entity_id { get; set; }
        public int circuit_id { get; set; } = 1;
    }

    [Serializable]
    public class BlueprintPosition
    {
        public float x { get; set; }
        public float y { get; set; }
    }

    [Serializable]
    public class BlueprintControlBehaviour
    {
        public List<BlueprintControlFilter> filters { get; } = new List<BlueprintControlFilter>();
    }

    [Serializable]
    public class BlueprintControlFilter
    {
        public BlueprintSignal signal { get; set; } = new BlueprintSignal();
        public int index { get; set; }
        public int count { get; set; }
    }
}