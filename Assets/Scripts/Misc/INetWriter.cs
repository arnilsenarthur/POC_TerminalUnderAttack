namespace TUA.Misc
{
    public interface INetWriter
    {
        void WriteByte(byte value);
        void WriteBoolean(bool value);
        void WriteInt32(int value);
        void WriteInt64(long value);
        void WriteSingle(float value);
        void WriteDouble(double value);
        void WriteString(string value);
    }
}
