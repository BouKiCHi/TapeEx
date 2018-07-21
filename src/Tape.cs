using System;
using System.IO;

class Tape
{
    const string ProgramTitle = "TapeEx";
    const string ProgramVersion = "1.00";

    private string OutputFilename;
    private string InputFilename;

    private long RecordStart;
    private long RecordLength;


    RunModeType RunMode = RunModeType.None;
    public enum RunModeType {
        None,
        Read,
        Write
    };

    public enum OptionType {
        None,
        Read,
        Write,
        Help,
        OutputFile,
        StartPosition,
        RecordLength,
        LongPulseWidth,
        DebugRough,
        DebugDetail
    }

    const int FormMax = 0x7FFF;
    const int FormMin = 0x0000;

    int FormMaxLevel = 0x400;
    int FormMinLevel = 0x100;
 
    private void WriteSample(WaveFormat w, int Width,int Sample) {
        for (var i = 0; i < Width; i++) {
            w.WriteSample(Sample);
        }
    }

    public bool OutputRawWave() {

        StreamReader sr = new StreamReader(InputFilename);

        var w = new WaveFormat();
        w.Open(OutputFilename, true);

        while (true) {
            var Line = sr.ReadLine();
            if (Line == null) break;
            var Data = Line.Split(',');
            var Form = Convert.ToInt32(Data[0]);
            var Width = Convert.ToInt32(Data[1]);

            var Sample = FormMin;
            if (Form == 1) Sample = FormMax;
            if (Form == -1) Sample = 0-(FormMax+1);

            WriteSample(w,Width,Sample);
        }

        sr.Dispose();
        w.Close();

        return true;
    }

    // txtからWAVファイルを作成
    public bool OutputDataWave() {

        StreamReader sr = new StreamReader(InputFilename);

        var w = new WaveFormat();
        w.Open(OutputFilename, true);
        SamplePerSecond = w.Rate;

        var HighValue = FormMax;
        var LowValue = 0-(FormMax+1);

        while (true) {
            var Line = sr.ReadLine();
            if (string.IsNullOrEmpty(Line)) break;
            var Data = Line.Split(',');
            var Form = Convert.ToInt32(Data[0]);
            var Width = Convert.ToInt32(Data[1]);

            StepSample(Width);

            if (Form == 2) {
                WriteSample(w,Width,LowValue);
                continue;
            }

            if (Form == -1) {
                WriteSample(w,Width,0);
                continue;
            }

            if (Form == 0) {
                var Left = Width-(25*7);
                // Wait + HL HL HL H
                WriteSample(w,Left,LowValue);
                for(var i=0; i < 3; i++) {
                    WriteSample(w,25,HighValue);
                    WriteSample(w,25,LowValue);
                }
                WriteSample(w,25,HighValue);
            }

            if (Form == 1) {
                var Left = Width-(47*3);
                // Wait + HL H
                WriteSample(w,Left,LowValue);
                WriteSample(w,47,HighValue);
                WriteSample(w,47,LowValue);
                WriteSample(w,47,HighValue);
            }

        }

        Console.WriteLine();

        sr.Dispose();
        w.Close();

        return true;
    }

    enum FormType {
        HiZ,
        High,
        Low,
        End
    }

    enum ModeType {
        NoSignal,
        WaitData,
        Pulse,
        DataEnd
    }

    WaveFormat Wav;
    StreamWriter Writer;


    ModeType CurrentMode = ModeType.NoSignal;

    long CurrentModeStartPosition = 0;
    int PulseCount = 0;
    FormType LastPulseForm = FormType.HiZ;

    // 現在のパルスの開始位置
    long LastPulseStart = 0;

    // 現在のパルスの幅
    long PulseFormCount = 0;

    // 現在のサンプルのレベル
    FormType CurrentForm = FormType.HiZ;
    // 
    FormType CurrentPulseForm = FormType.HiZ;

    // このレベルの幅のカウント
    int FormCount = 0;

    // 秒数表示
    int SamplePerSecond = 0;
    int SampleCount = 0;
    int Second = 0;

    // 進んだサンプル数
    long TotalSamples = 0;

    // データが1になるパルス幅
    int LongPulseWidth = 55;

    // 各パルスの記録
    int[] PulseWidthArray = new int[8];
    private bool DebugDetail = false;
    private bool DebugFlag = false;
    private long MaxDataLength = 4000;
    private long DataEndLength = 2000;

    private bool DataEndFlag = false;

    public bool OutputDataText() {

        Writer = new StreamWriter(OutputFilename);
        if (!OpenWaveFile()) return false;

        while (!Wav.ReadEof()) {
            var Sample = Wav.ReadSample();

            // 現在のレベルを認識
            if (FormMaxLevel <= Sample) CurrentForm = FormType.High;
            if (Sample <= 0 - FormMaxLevel) CurrentForm = FormType.Low;
            if (0 - FormMinLevel < Sample && Sample < FormMinLevel) CurrentForm = FormType.HiZ;

            // レベルの変化を検出
            FormChanges();

            // 特定のレベルが一定サンプル数が経過した場合、レベルが変化することにする
            if (LastPulseForm != CurrentPulseForm && 5 <= FormCount) {
                switch (CurrentMode) {
                    // 無信号状態からの変化
                    case ModeType.NoSignal:
                        OutputLevel(FormType.HiZ);
                        if (CurrentPulseForm == FormType.Low) SetWaitMode();
                        if (CurrentPulseForm == FormType.High) SetPulseMode();
                        break;
                    // 現在が待機区間
                    case ModeType.WaitData:
                        if (CurrentPulseForm == FormType.High) SetPulseMode();
                        break;
                    case ModeType.Pulse:
                        CountPulse();
                        break;
                }

                // 変化を適用
                LastPulseForm = CurrentPulseForm;
                PulseFormCount = FormCount;
            }

            PulseFormCount++;
            FormCount++;

            // データ終端を検出
            if (DataEndFlag) break;

            // 秒数を進める
            if (!StepSample()) break;
        }

        Console.WriteLine("");
        CloseWrite();

        return true;
    }

    private void CountPulse() {
        // パルス幅と現在の位置を保存
        // 01234567
        // LHLHLHLH
        long CurrentPulseStart = TotalSamples - FormCount;
        int PulseWidth = (int)(CurrentPulseStart - LastPulseStart);
        PulseWidthArray[PulseCount] = PulseWidth;
        if (DebugDetail) OutputMessage(string.Format("PulseWidth[{0}] = {1} at {2}", PulseCount,PulseWidth, RecordStart + LastPulseStart));

        LastPulseStart = CurrentPulseStart;
        PulseCount++;
        if (PulseCount == 4) {
            var FirstWidth = PulseWidthArray[1];
            var SecondWidth = PulseWidthArray[3];

            if (DebugDetail) OutputMessage(string.Format("Width {0} {1}", FirstWidth, SecondWidth));
            PulseWidth = (FirstWidth + SecondWidth) / 2;
            if (DebugDetail) OutputMessage(string.Format("PulseWidth:{0} {1}", PulseWidth, LastPulseStart));
            // 長い場合は1
            if (LongPulseWidth <= PulseWidth) {
                OutputLevel(FormType.High);
                SetWaitMode();
            }
        }
        if (PulseCount == 8) {
            // 0を出力
            OutputLevel(FormType.Low);
            SetWaitMode();
        }
    }

    private void FormChanges() {
        if (CurrentPulseForm == CurrentForm) return;
        // 最初以外はHiZにはならない
        if (CurrentForm == FormType.HiZ) return;

        if (DebugDetail) {
            OutputMessage(string.Format("FormChange:{0} -> {1} {2} -> {3} Width:{4}",
                CurrentPulseForm.ToString(),
                CurrentForm.ToString(),
                (TotalSamples + RecordStart) - FormCount,
                TotalSamples + RecordStart,
                FormCount
            ));
        }

        FormCount = 0;
        CurrentPulseForm = CurrentForm;
    }

    private void OutputMessage(string Message) {
        Writer.WriteLine(Message);
    }

    private void OutputLevel(FormType Level) {
        var Length = (TotalSamples - FormCount) - CurrentModeStartPosition;
        int LevelValue = -1;
        if (Level == FormType.High) LevelValue = 1;
        if (Level == FormType.Low) LevelValue = 0;
        if (Level == FormType.End) LevelValue = 2;

        var ModeStartPosition = TotalSamples - FormCount;

        if (Length > 0) {
            if (DebugFlag) Writer.WriteLine(string.Format("Mode Start = {0} End = {1}", 
                CurrentModeStartPosition + RecordStart, 
                ModeStartPosition + RecordStart));
            if (Length >= MaxDataLength) {
                Writer.WriteLine(string.Format("{0},{1}", 2, DataEndLength));
                DataEndFlag = true;
            } else {
                Writer.WriteLine(string.Format("{0},{1}", LevelValue, Length));
            }
        }
        CurrentModeStartPosition = ModeStartPosition;
    }

    private void SetPulseMode() {
        CountPulse();
        CurrentMode = ModeType.Pulse;
        if (DebugDetail) Writer.WriteLine(string.Format("PulseMode at {0}", GetCurrentFilePosition()));
    }

    private long GetCurrentFilePosition() {
        return (TotalSamples + RecordStart);
    }

    private void SetWaitMode() {
        ResetPulseCount();
        CurrentMode = ModeType.WaitData;
        if (DebugDetail) Writer.WriteLine(string.Format("WaitMode at {0}", GetCurrentFilePosition()));
    }

    private void ResetPulseCount() {
        PulseCount = 0;
        LastPulseStart = TotalSamples - FormCount;
    }

    private bool StepSample(int Step = 1) {
        SampleCount+=Step;
        bool Update = false;
        while(SampleCount >= SamplePerSecond) {
            Update = true;
            SampleCount -= SamplePerSecond;
            Second++;
        }

        if (Update) ShowCurrentTime();

        // 指定の長さ以上で終了
        TotalSamples++;
        if (RecordLength > 0 && RecordLength < TotalSamples) {
            return false;
        }
        return true;
    }

    private void ShowCurrentTime() {
        Console.Write("{0:00}:{1:00}\r", Second / 60, Second % 60);
    }

    // 波形ファイルを開く
    private bool OpenWaveFile() {
        Wav = new WaveFormat();

        Wav.Open(InputFilename, false);
        Wav.ReadHeader();
        
        if (Wav.Bits != 16 || Wav.Channels != 1) {
            Console.WriteLine("Not Support WAVE format.");
            CloseWrite();
            return false;
        }

        SamplePerSecond = Wav.Rate;
        var SamplePerByte = Wav.Bits / 8;

        // 波形を開始位置までスキップ
        if (RecordStart > 0) Wav.Position = Wav.Position + (RecordStart * SamplePerByte);

        return true;
    }

    private void CloseWrite() {
            Writer.Dispose();
            Wav.Close();
    }

    public bool Run(string[] args)
    {
        var mo = new MiniOption();
        AddOptionData(mo);

        if (!mo.Parse(args)) return false;

        if (mo.Files.Count < 1) {
            Usage();
            return false;
        }

        InputFilename = mo.Files[0];

        if (!CheckOption(mo)) return false;

        // 拡張子判別
        if (RunMode == RunModeType.None) {
            var e = Path.GetExtension(InputFilename);
            var IsWavFile = (string.IsNullOrEmpty(e) || e.ToLower() == ".wav");
            RunMode = IsWavFile ? RunModeType.Read : RunMode = RunModeType.Write;
        }

        if (string.IsNullOrEmpty(OutputFilename)) OutputFilename = "output." + (RunMode == RunModeType.Read ? "txt" : "wav");

        Console.WriteLine("Output : " + OutputFilename);

        if (RunMode != RunModeType.Read) {
            Console.WriteLine("Write WAV File From Text...");
            if (!OutputDataWave()) return false;
        } else {
            Console.WriteLine("Write Text From WAV File ...");
            if (!OutputDataText()) return false;
        }

        Console.WriteLine("Done");

        return true;
    }

    private static void AddOptionData(MiniOption miniopt) {
        miniopt.AddOptionDefines(new MiniOption.DefineData[] {
            new MiniOption.DefineData((int)OptionType.Read,"r","read",false),
            new MiniOption.DefineData((int)OptionType.Write,"w","write",false),
            new MiniOption.DefineData((int)OptionType.OutputFile,"o","output",true),
            new MiniOption.DefineData((int)OptionType.StartPosition,"s","start",true),
            new MiniOption.DefineData((int)OptionType.RecordLength,"l","length",true),
            new MiniOption.DefineData((int)OptionType.LongPulseWidth,null,"pulse",true),
            new MiniOption.DefineData((int)OptionType.DebugRough,null,"debug",false),
            new MiniOption.DefineData((int)OptionType.DebugDetail,null,"detail",false),
            new MiniOption.DefineData((int)OptionType.Help,"?",null,false),
            new MiniOption.DefineData((int)OptionType.Help,"h",null,false)
        });
    }

    
    public void Usage() {
        Console.WriteLine("{0} ver {1}", ProgramTitle, ProgramVersion);
        Console.WriteLine("Usage TapeEx [Options...] <input.wav|input.txt>");
        Console.WriteLine();
        Console.WriteLine(" Options...");
        Console.WriteLine(" -r,--read  Read wave file");
        Console.WriteLine(" -w,--write   Write wave file");
        Console.WriteLine(" -o,--output <file>  Set output filename");
        Console.WriteLine(" -s,--start <pos>  Set start position");
        Console.WriteLine(" -l,--length <pos>  Set record length");
        Console.WriteLine(" --pulse <width>  Set long pulse width");
        Console.WriteLine();
        Console.WriteLine(" --debug  Debug mode");
        Console.WriteLine(" --detail Debug detail mode");
        Console.WriteLine();
        Console.WriteLine(" -h,-?,--help  This one");
    }

    public bool CheckOption(MiniOption miniopt) {
        foreach (var o in miniopt.Result) {
            switch (o.Type) {
                case (int)OptionType.Read:
                    RunMode = RunModeType.Read;
                    break;

                case (int)OptionType.Write:
                    RunMode = RunModeType.Write;
                    break;

                case (int)OptionType.OutputFile:
                    OutputFilename = o.Value;
                    break;

                case (int)OptionType.StartPosition:
                    Console.WriteLine("Start:" + o.Value);
                    RecordStart = Convert.ToInt32(o.Value);
                    break;

                case (int)OptionType.RecordLength:
                    Console.WriteLine("End:" + o.Value);
                    RecordLength = Convert.ToInt32(o.Value);
                    break;

                case (int)OptionType.LongPulseWidth:
                    Console.WriteLine("LongPulseWidth:" + o.Value);
                    LongPulseWidth = Convert.ToInt32(o.Value);
                    break;

                case (int)OptionType.DebugRough:
                    DebugFlag = true;
                    break;

                case (int)OptionType.DebugDetail:
                    DebugDetail = true;
                    break;

                case (int)OptionType.Help:
                    Usage();
                    return false;
            }
        }
        return true;
    }


}