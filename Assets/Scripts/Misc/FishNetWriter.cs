using FishNet.Serializing;

namespace TUA.Misc
{
    public class FishNetWriter : INetWriter
    {
        private readonly Writer _writer;
        public FishNetWriter(Writer writer)
        {
            _writer = writer;
        }
        
        public void WriteByte(byte value) => _writer.WriteByte(value);
        public void WriteBoolean(bool value) => _writer.WriteBoolean(value);
        public void WriteInt32(int value) => _writer.WriteInt32(value);
        public void WriteInt64(long value) => _writer.WriteInt64(value);
        public void WriteSingle(float value) => _writer.WriteSingle(value);
        public void WriteDouble(double value) => _writer.WriteDouble(value);
        public void WriteString(string value) => _writer.WriteString(value ?? string.Empty);
    }
}
