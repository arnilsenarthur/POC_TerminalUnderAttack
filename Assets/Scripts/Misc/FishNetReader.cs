using FishNet.Serializing;
namespace TUA.Misc
{
    public class FishNetReader : INetReader
    {
        private readonly Reader _reader;
        public FishNetReader(Reader reader)
        {
            _reader = reader;
        }
        
        public byte ReadByte() => _reader.ReadByte();
        public bool ReadBoolean() => _reader.ReadBoolean();
        public int ReadInt32() => _reader.ReadInt32();
        public long ReadInt64() => _reader.ReadInt64();
        public float ReadSingle() => _reader.ReadSingle();
        public double ReadDouble() => _reader.ReadDouble();
        public string ReadString() => _reader.ReadStringAllocated() ?? string.Empty;
    }
}
