class BinaryBase {
    
    protected byte[] StringArray(string Text, System.Text.Encoding Encode = null) {
        if (Encode == null) return  System.Text.Encoding.ASCII.GetBytes(Text);
        return Encode.GetBytes(Text);
    }

    protected byte[] ByteArray(int Value) {
        var b = new byte[1];
        b[0] = (byte)(Value & 0xff);
        return b;
    }

    protected byte[] WordArray(int Value) {
        var b = new byte[2];
        b[0] = (byte)(Value & 0xff);
        b[1] = (byte)((Value >> 8) & 0xff);
        return b;
    }

    protected byte[] LongArray(long Value) {
        var b = new byte[4];
        b[0] = (byte)(Value & 0xff);
        b[1] = (byte)((Value >> 8) & 0xff);
        b[2] = (byte)((Value >> 16) & 0xff);
        b[3] = (byte)((Value >> 24) & 0xff);
        return b;
    }

    protected int ByteValue(byte[] Data) {
        return (byte)Data[0];
    }

    protected int WordValue(byte[] Data)
    {
        ulong result = Data[0];
        result |= ((ulong)Data[1] << 8);
        return (int)result;
    }

    protected short ShortValue(byte[] Data,int Pos)
    {
        ulong result = Data[Pos];
        result |= ((ulong)Data[Pos+1] << 8);
        return (short)result;
    }

    protected int LongValue(byte[] Data)
    {
        ulong result = Data[0];
        result |= ((ulong)Data[1] << 8);
        result |= ((ulong)Data[2] << 16);
        result |= ((ulong)Data[3] << 24);
        return (int)result;
    }
}
