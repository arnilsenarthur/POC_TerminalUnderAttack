namespace TUA.Misc
{
    public interface INetReader
    {
        byte ReadByte();
        bool ReadBoolean();
        int ReadInt32();
        long ReadInt64();
        float ReadSingle();
        double ReadDouble();
        string ReadString();
    }
}
