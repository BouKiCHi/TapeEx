using System;
using System.IO;

class WaveFormat : BinaryBase {

    public int Rate = 44100;
    public int Bits = 16;
    public int Channels = 1;
    public long FileLength;
    private long DataStartPosition;

    bool WriteMode = false;

    bool WriteHeaderData = false;

    BinaryWriter WriteFile;
    BinaryReader ReadFile;
    
    FileStream WavFileStream;

    byte[] ReadSampleBuffer;
    int ReadSamplePosition;

    public long Position { 
        get {
        return WavFileStream.Position;
        }
        set {
            WavFileStream.Position = value;
        } 
     }

    private void WriteTagWithLength(string Tag,long DataPosition,long Length) {
        WriteFile.BaseStream.Position = DataPosition;
        // WriteData.Seek((int)DataPosition,SeekOrigin.Begin);
        WriteFile.Write(StringArray(Tag));
        WriteFile.Write(LongArray(Length - DataPosition));
    }

    // Length = 現在のファイルの長さ
    private void WriteRIFF(long Length) {
        WriteTagWithLength("RIFF",0,Length);
    }

    // Length = この構造体の長さ
    private void WriteDataLength(long DataPosition,long Length) {
        WriteTagWithLength("data",DataPosition,Length);
    }

    private string GetString(byte[] Data, System.Text.Encoding Encode = null) {
        if (Encode == null) 
            return System.Text.Encoding.ASCII.GetString(Data);
        return Encode.GetString(Data);
    }

    private bool CheckByteString(byte[] Data,string CompareText, System.Text.Encoding Encode = null) {
        string Text = GetString(Data,Encode);
        return (Text == CompareText);
    }

    public void ReadHeader() {
        
        if (!CheckByteString(ReadFile.ReadBytes(4),"RIFF")) {
            Console.WriteLine("not RIFF format!");
            return;
        }

        ReadFile.ReadInt32(); // FileSize;

        if (!CheckByteString(ReadFile.ReadBytes(4),"WAVE")) {
            Console.WriteLine("not WAVE format!");
            return;
        }

        var NextTag = SeekTag("fmt ");
        if (NextTag < 0) {
            Console.WriteLine("End of File detected.");
            return;
        }
        // read format info... 
        var FormatId = ReadFile.ReadInt16();
        Channels = ReadFile.ReadInt16();
        Rate = ReadFile.ReadInt32();
        ReadFile.ReadInt32(); // Speed
        ReadFile.ReadInt16(); // Block
        Bits = ReadFile.ReadInt16();

        Console.WriteLine(string.Format("FormatId:{0} Channels:{1} Rate:{2} Bits:{3}",
            FormatId,Channels,Rate,Bits));

        ReadFile.BaseStream.Position = NextTag;

        NextTag = SeekTag("data");
        if (NextTag < 0) {
            Console.WriteLine("End of File detected.");
            return;
        }
    }

    public bool ReadEof() {
        return ReadFile.BaseStream.Position >= ReadFile.BaseStream.Length;
    }

    public int ReadSample() {
        if (ReadSampleBuffer == null || ReadSamplePosition >= 16384) {
            ReadSampleBuffer = ReadFile.ReadBytes(16384);
            ReadSamplePosition = 0;
        }

        var r = ShortValue(ReadSampleBuffer,ReadSamplePosition);
        ReadSamplePosition += 2;
        return r;
    }


    private long SeekTag(string TagName) {
        while(true) {
            var FormatData = ReadFile.ReadBytes(4);
            if (FormatData.Length < 4) {
                Console.WriteLine("End of File detected.");
                return -1;
            }
            var FormatName = GetString(FormatData);
            var FormatSize = ReadFile.ReadInt32();
            var BasePosition = ReadFile.BaseStream.Position;

            if (FormatName != TagName) {
                // Console.WriteLine(string.Format("Skip {0} format",FormatName));
                ReadFile.BaseStream.Position = BasePosition + FormatSize;
                continue;
            }
            return BasePosition + FormatSize;
        }
    }

    public void Open(string Filename,bool WriteMode) {
        this.WriteMode = WriteMode;
        WriteHeaderData = false;
        if (WriteMode) {
            WavFileStream = new FileStream(Filename, FileMode.Create);
            WriteFile = new BinaryWriter(WavFileStream);
            return;
        }

        WavFileStream = new FileStream(Filename, FileMode.Open,FileAccess.Read);
        ReadFile = new BinaryReader(WavFileStream);
    }

    public void WriteHeader() {
        WriteHeaderData = true;
        WriteRIFF(0);
        WriteFile.Write(StringArray("WAVE"));
        WriteFile.Write(StringArray("fmt "));
        WriteFile.Write(LongArray(16)); // fmt size
        WriteFile.Write(WordArray(1)); // FormatId
        WriteFile.Write(WordArray(Channels)); // Channels
        WriteFile.Write(LongArray(Rate)); // サンプリングレート
        WriteFile.Write(LongArray(Rate * Channels * (Bits / 8))); // データ速度
        WriteFile.Write(WordArray(Channels * (Bits / 8))); // ブロックサイズ
        WriteFile.Write(WordArray(Bits)); // Bits
        
        DataStartPosition = WriteFile.BaseStream.Position;
        FileLength = WriteFile.BaseStream.Length;

        WriteDataLength(DataStartPosition,FileLength);
    }

    public void WriteSample(int Sample) {
        if (!WriteHeaderData) {
            WriteHeader();
        }

        WriteFile.Write(WordArray(Sample)); 
    }

   public void Close() {
        if (WriteMode) {
            FileLength = WriteFile.BaseStream.Length;
            WriteDataLength(DataStartPosition,FileLength);
            WriteRIFF(FileLength);
            WriteFile.Close();
            WavFileStream.Close();
            return;
        }

        ReadFile.Close();
        WavFileStream.Close();
    }
}