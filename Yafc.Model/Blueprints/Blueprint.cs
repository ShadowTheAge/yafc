using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Yafc.Model;
using Yafc.UI;

namespace Yafc.Blueprints;

[Serializable]
public class BlueprintString(string blueprintName) {
    public Blueprint blueprint { get; } = new Blueprint(blueprintName);
    private static readonly byte[] header = [0x78, 0xDA];
    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public string ToBpString() {
        if (InputSystem.Instance.control) {
            return ToJson();
        }

        byte[] sourceBytes = JsonSerializer.SerializeToUtf8Bytes(this, jsonSerializerOptions);
        using MemoryStream memory = new MemoryStream();
        memory.Write(header);
        using (DeflateStream compress = new DeflateStream(memory, CompressionLevel.Optimal, true)) {
            compress.Write(sourceBytes);
        }

        memory.Write(GetChecksum(sourceBytes, sourceBytes.Length));

        return "0" + Convert.ToBase64String(memory.ToArray());
    }

    private static byte[] GetChecksum(byte[] buffer, int length) {
        int a = 1, b = 0;

        for (int counter = 0; counter < length; ++counter) {
            a = (a + buffer[counter]) % 65521;
            b = (b + a) % 65521;
        }

        int checksum = (b * 65536) + a;
        byte[] intBytes = BitConverter.GetBytes(checksum);
        Array.Reverse(intBytes);

        return intBytes;
    }

    public string ToJson() {
        byte[] sourceBytes = JsonSerializer.SerializeToUtf8Bytes(this, jsonSerializerOptions);
        using MemoryStream memory = new MemoryStream(sourceBytes);
        using StreamReader reader = new StreamReader(memory);

        return reader.ReadToEnd();
    }
}

[Serializable]
public class Blueprint(string label) {
    public const int VERSION = 0x01000000;

    public string item { get; set; } = "blueprint";
    public string label { get; set; } = label;
    public List<BlueprintEntity> entities { get; } = [];
    public List<BlueprintIcon> icons { get; } = [];
    public int version { get; set; } = VERSION;
}

[Serializable]
public class BlueprintIcon {
    public int index { get; set; }
    public BlueprintSignal signal { get; set; } = new BlueprintSignal();
}

[Serializable]
public class BlueprintSignal {
    public string? name { get; set; }
    public string? type { get; set; }

    public void Set(Goods goods) {
        if (goods is Special sp) {
            type = "virtual";
            name = sp.virtualSignal;
        }
        else if (goods is Fluid fluid) {
            type = "fluid";
            name = fluid.originalName;
        }
        else {
            type = "item";
            name = goods.name;
        }
    }
}

[Serializable]
public class BlueprintEntity {
    [JsonPropertyName("entity_number")] public int index { get; set; }
    public string? name { get; set; }
    public BlueprintPosition position { get; set; } = new BlueprintPosition();
    public int direction { get; set; }
    public string? recipe { get; set; }
    [JsonPropertyName("control_behavior")] public BlueprintControlBehavior? controlBehavior { get; set; }
    public BlueprintConnection? connections { get; set; }
    [JsonPropertyName("request_filters")] public List<BlueprintRequestFilter> requestFilters { get; } = [];
    public Dictionary<string, int>? items { get; set; }

    public void Connect(BlueprintEntity other, bool red = true, bool secondPort = false, bool targetSecond = false) {
        ConnectSingle(other, red, secondPort, targetSecond);
        other.ConnectSingle(this, red, targetSecond, secondPort);
    }

    private void ConnectSingle(BlueprintEntity other, bool red = true, bool secondPort = false, bool targetSecond = false) {
        connections ??= new BlueprintConnection();
        BlueprintConnectionPoint port;
        if (secondPort) {
            port = connections.p2 ?? (connections.p2 = new BlueprintConnectionPoint());
        }
        else {
            port = connections.p1 ?? (connections.p1 = new BlueprintConnectionPoint());
        }

        var list = red ? port.red : port.green;
        list.Add(new BlueprintConnectionData { entityId = other.index, circuitId = targetSecond ? 2 : 1 });
    }
}

[Serializable]
public class BlueprintRequestFilter {
    public string? name { get; set; }
    public int index { get; set; }
    public int count { get; set; }
}

[Serializable]
public class BlueprintConnection {
    [JsonPropertyName("1")] public BlueprintConnectionPoint? p1 { get; set; }
    [JsonPropertyName("2")] public BlueprintConnectionPoint? p2 { get; set; }
}

[Serializable]
public class BlueprintConnectionPoint {
    public List<BlueprintConnectionData> red { get; } = [];
    public List<BlueprintConnectionData> green { get; } = [];
}

[Serializable]
public class BlueprintConnectionData {
    [JsonPropertyName("entity_id")] public int entityId { get; set; }
    [JsonPropertyName("circuit_id")] public int circuitId { get; set; } = 1;
}

[Serializable]
public class BlueprintPosition {
    public float x { get; set; }
    public float y { get; set; }
}

[Serializable]
public class BlueprintControlBehavior {
    public List<BlueprintControlFilter> filters { get; } = [];
}

[Serializable]
public class BlueprintControlFilter {
    public BlueprintSignal signal { get; set; } = new BlueprintSignal();
    public int index { get; set; }
    public int count { get; set; }
}
