using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.LameWrapper;
using Ciribob.DCS.SimpleRadio.Standalone.Common.NetCoreServer;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.NAudioLame;
#pragma warning disable IDE1006 // Naming Styles

/// <summary>Delegate for receiving output messages</summary>
/// <param name="text">Text to output</param>
/// <remarks>
///     Output from the LAME library is very limited.  At this stage only a few direct calls will result in output. No
///     output is normally generated during encoding.
/// </remarks>
public delegate void OutputHandler(string text);

/// <summary>Delegate for progress feedback from encoder</summary>
/// <param name="writer"><see cref="LameMP3FileWriter" /> instance that the progress update is for</param>
/// <param name="inputBytes">Total number of bytes passed to encoder</param>
/// <param name="outputBytes">Total number of bytes written to output</param>
/// <param name="finished">True if encoding process is completed</param>
public delegate void ProgressHandler(object writer, long inputBytes, long outputBytes, bool finished);

public class LameMP3FileWriter : Stream
{
    #region Progress

    /// <summary>Call any registered OnProgress handlers</summary>
    /// <param name="finished">True if called at end of output</param>
    protected void RaiseProgress(bool finished)
    {
        var timeDelta = DateTime.Now - _lastProgress;
        if (finished || timeDelta.TotalMilliseconds >= _minProgressTime)
        {
            _lastProgress = DateTime.Now;
            OnProgress?.Invoke(this, _inputByteCount, _outputByteCount, finished);
        }
    }

    #endregion

    #region Supporting classes

    /// <summary>Union class for fast buffer conversion</summary>
    /// <remarks>
    ///     <para>
    ///         Because of the way arrays work in .NET, all of the arrays will have the same
    ///         length value.  To prevent unaware code from trying to read/write from out of
    ///         bounds, allocation is done at the grain of the Least Common Multiple of the
    ///         sizes of the contained types.  In this case the LCM is 8 bytes - the size of
    ///         a double or a long - which simplifies allocation.
    ///     </para>
    ///     <para>
    ///         This means that when you ask for an array of 500 bytes you will actually get
    ///         an array of 504 bytes total - 63 doubles.  Any code that uses the length of
    ///         the array will see only 63 bytes, shorts, etc.
    ///     </para>
    ///     <para>
    ///         CodeAnalysis does not like this class, with good reason.  It should never be
    ///         exposed beyond the scope of the MP3FileWriter.
    ///     </para>
    /// </remarks>
    // uncomment to suppress CodeAnalysis warnings for the ArrayUnion class:
    [StructLayout(LayoutKind.Explicit)]
    private class ArrayUnion
    {
        /// <summary>Length of the byte array</summary>
        [FieldOffset(0)] public readonly int nBytes;

        /// <summary>Array of unsigned 8-bit integer values, length will be misreported</summary>
        [FieldOffset(16)] public readonly byte[] bytes;

        /// <summary>Array of signed 16-bit integer values, length will be misreported</summary>
        [FieldOffset(16)] public readonly short[] shorts;

        /// <summary>Array of signed 32-bit integer values, length will be misreported</summary>
        [FieldOffset(16)] public readonly int[] ints;

        /// <summary>Array of signed 64-bit integer values, length will be correct</summary>
        [FieldOffset(16)] public readonly long[] longs;

        /// <summary>Array of signed 32-bit floating point values, length will be misreported</summary>
        [FieldOffset(16)] public readonly float[] floats;

        /// <summary>Array of signed 64-bit floating point values, length will be correct</summary>
        /// <remarks>This is the actual array allocated by the constructor</remarks>
        [FieldOffset(16)] public readonly double[] doubles;

        // True sizes of the various array types, calculated from number of bytes

        /// <summary>Actual length of the 'shorts' member array</summary>
        public int nShorts => nBytes / 2;

        /// <summary>Actual length of the 'ints' member array</summary>
        public int nInts => nBytes / 4;

        /// <summary>Actual length of the 'longs' member array</summary>
        public int nLongs => nBytes / 8;

        /// <summary>Actual length of the 'floats' member array</summary>
        public int nFloats => nBytes / 4;

        /// <summary>Actual length of the 'doubles' member array</summary>
        public int nDoubles => doubles.Length;

        /// <summary>Initialize array to hold the requested number of bytes</summary>
        /// <param name="reqBytes">Minimum byte count of array</param>
        /// <remarks>
        ///     Since all arrays will have the same apparent count, allocation
        ///     is done on the array with the largest data type.  This helps
        ///     to prevent out-of-bounds reads and writes by methods that do
        ///     not know about the union.
        /// </remarks>
        public ArrayUnion(int reqBytes)
        {
            // Calculate smallest number of doubles required to store the 
            // requested byte count
            var reqDoubles = (reqBytes + 7) / 8;

            doubles = new double[reqDoubles];
            nBytes = reqDoubles * 8;
        }

        private ArrayUnion()
        {
            throw new Exception("Default constructor cannot be called for ArrayUnion");
        }
    }

    #endregion

    #region State

    // LAME library context 
    private LibMp3Lame _lame;

    /// <summary>
    ///     Retrieve the last captured result from calling a LAME dll method
    /// </summary>
    public int LastLameResult => _lame.LastLameError;

    // Format of input wave data
    private readonly WaveFormat _inputFormat;

    // Output stream to write encoded data to
    private Stream _outStream;

    // Flag to control whether we should dispose of output stream 
    private readonly bool _disposeOutput;

    private readonly ArrayUnion _inBuffer;

    private int inPosition;

    protected byte[] _outBuffer;

    private long _inputByteCount;
    private long _outputByteCount;

    private delegate int delEncode();

    private readonly delEncode _encode;

    // Progress

    private int _minProgressTime = 100;

    /// <summary>Minimimum time between progress events in ms, or 0 for no limit</summary>
    /// <remarks>Defaults to 100ms</remarks>
    public int MinProgressTime
    {
        get => _minProgressTime;
        set => _minProgressTime = Math.Max(0, value);
    }

    /// <summary>Called when data is written to the output file from Encode or Flush</summary>
    public event ProgressHandler OnProgress;

    private DateTime _lastProgress = DateTime.Now;

    // Pre-initialisation

    #endregion

    #region Lifecycle

    /// <summary>Create MP3FileWriter to write to a file on disk</summary>
    /// <param name="outFileName">Name of file to create</param>
    /// <param name="format">Input WaveFormat</param>
    /// <param name="quality">LAME quality preset</param>
    /// <param name="id3">Optional ID3 data block</param>
    public LameMP3FileWriter(string outFileName, WaveFormat format, LAMEPreset quality, ID3TagData id3 = null)
        : this(File.Create(outFileName), format, quality, id3)
    {
        _disposeOutput = true;
    }

    /// <summary>Create MP3FileWriter to write to supplied stream</summary>
    /// <param name="outStream">Stream to write encoded data to</param>
    /// <param name="format">Input WaveFormat</param>
    /// <param name="quality">LAME quality preset</param>
    /// <param name="id3">Optional ID3 data block</param>
    public LameMP3FileWriter(Stream outStream, WaveFormat format, LAMEPreset quality, ID3TagData id3 = null)
        : this(outStream, format, new LameConfig { Preset = quality, ID3 = id3 })
    {
    }

    /// <summary>Create MP3FileWriter to write to a file on disk</summary>
    /// <param name="outFileName">Name of file to create</param>
    /// <param name="format">Input WaveFormat</param>
    /// <param name="bitRate">Output bit rate in kbps</param>
    /// <param name="id3">Optional ID3 data block</param>
    public LameMP3FileWriter(string outFileName, WaveFormat format, int bitRate, ID3TagData id3 = null)
        : this(File.Create(outFileName), format, bitRate, id3)
    {
        _disposeOutput = true;
    }

    /// <summary>Create MP3FileWriter to write to supplied stream</summary>
    /// <param name="outStream">Stream to write encoded data to</param>
    /// <param name="format">Input WaveFormat</param>
    /// <param name="bitRate">Output bit rate in kbps</param>
    /// <param name="id3">Optional ID3 data block</param>
    public LameMP3FileWriter(Stream outStream, WaveFormat format, int bitRate, ID3TagData id3 = null)
        : this(outStream, format, new LameConfig { BitRate = bitRate, ID3 = id3 })
    {
    }

    /// <summary>Create MP3FileWriter to write to a file on disk</summary>
    /// <param name="outFileName">Name of file to create</param>
    /// <param name="format">Input WaveFormat</param>
    /// <param name="config">LAME configuration</param>
    public LameMP3FileWriter(string outFileName, WaveFormat format, LameConfig config)
        : this(File.Create(outFileName), format, config)
    {
        _disposeOutput = true;
    }

    /// <summary>Create MP3FileWriter to write to supplied stream</summary>
    /// <param name="outStream">Stream to write encoded data to</param>
    /// <param name="format">Input WaveFormat</param>
    /// <param name="config">LAME configuration</param>
    public LameMP3FileWriter(Stream outStream, WaveFormat format, LameConfig config)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        // check for unsupported wave formats
        if (format.Channels != 1 && format.Channels != 2)
            throw new ArgumentException($"Unsupported number of channels {format.Channels}", nameof(format));
        if (format.Encoding != WaveFormatEncoding.Pcm && format.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException($"Unsupported encoding format {format.Encoding}", nameof(format));
        if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample != 16)
            throw new ArgumentException($"Unsupported PCM sample size {format.BitsPerSample}", nameof(format));
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample != 32)
            throw new ArgumentException($"Unsupported Float sample size {format.BitsPerSample}", nameof(format));
        if (format.SampleRate < 8000 || format.SampleRate > 48000)
            throw new ArgumentException($"Unsupported Sample Rate {format.SampleRate}", nameof(format));

        // select encoder function that matches data format
        if (format.Encoding == WaveFormatEncoding.Pcm)
        {
            if (format.Channels == 1)
                _encode = Encode_pcm_16_mono;
            else
                _encode = Encode_pcm_16_stereo;
        }
        else
        {
            if (format.Channels == 1)
                _encode = Encode_float_mono;
            else
                _encode = Encode_float_stereo;
        }

        // Set base properties
        _inputFormat = format;
        _outStream = outStream ?? throw new ArgumentNullException(nameof(outStream));
        _disposeOutput = false;

        // Allocate buffers based on sample rate
        _inBuffer = new ArrayUnion(format.AverageBytesPerSecond);
        _outBuffer = new byte[format.SampleRate * 5 / 4 + 7200];

        // Initialize lame library
        _lame = config.ConfigureDLL(format);

        if (config.ID3 != null)
            ApplyID3Tag(config.ID3);

        _lame.InitParams();
    }

    /// <summary>Dispose of object</summary>
    /// <param name="disposing">True if called from destructor, false otherwise</param>
    protected override void Dispose(bool disposing)
    {
        if (_lame != null && _outStream != null)
            Flush();

        _lame?.Dispose();
        _lame = null;

        if (_disposeOutput)
        {
            _outStream?.Dispose();
            _outStream = null;
        }

        base.Dispose(disposing);
    }

    #endregion

    #region Encoding

    private int Encode_pcm_16_mono()
    {
        return _lame.Write(_inBuffer.shorts, inPosition / 2, _outBuffer, _outBuffer.Length, true);
    }

    private int Encode_pcm_16_stereo()
    {
        return _lame.Write(_inBuffer.shorts, inPosition / 2, _outBuffer, _outBuffer.Length, false);
    }

    private int Encode_float_mono()
    {
        return _lame.Write(_inBuffer.floats, inPosition / 4, _outBuffer, _outBuffer.Length, true);
    }

    private int Encode_float_stereo()
    {
        return _lame.Write(_inBuffer.floats, inPosition / 4, _outBuffer, _outBuffer.Length, false);
    }

    private void Encode()
    {
        if (_outStream == null || _lame == null)
            throw new InvalidOperationException("Output stream closed.");

        if (inPosition < _inputFormat.Channels * 2)
            return;

        // send to encoder
        var rc = _encode();

        if (rc > 0)
        {
            _outStream.Write(_outBuffer, 0, rc);
            _outputByteCount += rc;
        }

        _inputByteCount += inPosition;
        inPosition = 0;

        RaiseProgress(false);
    }

    #endregion

    #region Stream implementation

    /// <summary>Write-only stream.  Always false.</summary>
    public override bool CanRead => false;

    /// <summary>Non-seekable stream.  Always false.</summary>
    public override bool CanSeek => false;

    /// <summary>True when encoder can accept more data</summary>
    public override bool CanWrite => _outStream != null && _lame != null;

    /// <summary>Dummy Position.  Always 0.</summary>
    public override long Position
    {
        get => 0;
        set => throw new NotImplementedException();
    }

    /// <summary>Dummy Length.  Always 0.</summary>
    public override long Length => 0;

    public override void Write(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty)
        {
            
            var blockSize = Math.Min(_inBuffer.nBytes - inPosition, bytes.Length);

            var sourceSpan = bytes.Slice(0, blockSize);
            var targetSpan = MemoryMarshal.Cast<double, byte>(_inBuffer.doubles.AsSpan()).Slice(inPosition, blockSize);
            sourceSpan.CopyTo(targetSpan);

            inPosition += blockSize;

            if (inPosition >= _inBuffer.nBytes)
                Encode();

            bytes = bytes.Slice(sourceSpan.Length);
        }
    }

    /// <summary>Add data to output buffer, sending to encoder when buffer full</summary>
    /// <param name="buffer">Source buffer</param>
    /// <param name="offset">Offset of data in buffer</param>
    /// <param name="count">Length of data</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    /// <summary>Finalise compression, add final output to output stream and close encoder</summary>
    public override void Flush()
    {
        // write remaining data
        if (inPosition > 0)
            Encode();

        // finalize compression
        var rc = _lame.Flush(_outBuffer, _outBuffer.Length);
        if (rc > 0)
        {
            _outStream.Write(_outBuffer, 0, rc);
            _outputByteCount += rc;
        }

        // report progress
        RaiseProgress(true);

        if (_lame.WriteVBRTag) UpdateLameTagFrame();

        // Cannot continue after flush, so clear output stream
        if (_disposeOutput)
            _outStream.Dispose();
        _outStream = null;
    }

    /// <summary>Get the VBR tag frame from LAME and write to stream if possible</summary>
    /// <returns>True if tag frame written to stream, else false</returns>
    /// <remarks>Based on the LAME source: https://sourceforge.net/p/lame/svn/HEAD/tree/trunk/lame/Dll/BladeMP3EncDLL.c#l816 </remarks>
    private bool UpdateLameTagFrame()
    {
        if (_outStream == null || !_outStream.CanSeek || !_outStream.CanRead || !_outStream.CanWrite)
            return false;

        var strmPos = _outStream.Position;
        try
        {
            var frame = _lame.GetLAMETagFrame();
            if (frame == null || frame.Length < 4)
                return false;

            if (SkipId3v2(frame.Length) != 0)
                return false;

            _outStream.Write(frame, 0, frame.Length);

            return true;
        }
        finally
        {
            _outStream.Position = strmPos;
        }
    }

    /// <summary>Position the output stream at the start of the first frame after the ID3 frame if any.</summary>
    /// <param name="framesize">Size of frame</param>
    /// <returns>0 on success, non-zero on failure</returns>
    /// <remarks>
    ///     Base algorithm copied from the LAME source:
    ///     https://sourceforge.net/p/lame/svn/HEAD/tree/trunk/lame/Dll/BladeMP3EncDLL.c#l768
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Sometimes you just have to catch them all.")]
    private int SkipId3v2(int framesize)
    {
        try
        {
            _outStream.Position = 0;
        }
        catch
        {
            return -2;
        }

        var buffer = new byte[10];
        var rc = _outStream.Read(buffer, 0, 10);
        if (rc != 10)
            return -3;
        var id3v2TagSize = 0;
        if (buffer[0] == (byte)'I' || buffer[1] == (byte)'D' || buffer[2] == (byte)'3')
            id3v2TagSize =
            (
                ((buffer[6] & 0x7f) << 21) |
                ((buffer[7] & 0x7f) << 14) |
                ((buffer[8] & 0x7f) << 7) |
                (buffer[9] & 0x7f)
            ) + 10;
        _outStream.Position = id3v2TagSize;

        // maybeSyncWord
        rc = _outStream.Read(buffer, 0, 4);
        if (rc != 4 || buffer[0] != 0xFF || (buffer[1] & 0xE0) != 0xE0)
            return -1;

        _outStream.Position = id3v2TagSize + framesize;

        // maybeSyncWord
        rc = _outStream.Read(buffer, 0, 4);
        if (rc != 4 || buffer[0] != 0xFF || (buffer[1] & 0xE0) != 0xE0)
            return -1;

        _outStream.Position = id3v2TagSize;
        return 0;
    }

    /// <summary>Reading not supported.  Throws NotImplementedException.</summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    /// <summary>Setting length not supported.  Throws NotImplementedException.</summary>
    /// <param name="value">Length value</param>
    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    /// <summary>Seeking not supported.  Throws NotImplementedException.</summary>
    /// <param name="offset">Seek offset</param>
    /// <param name="origin">Seek origin</param>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ID3 support

    /// <summary>Setup ID3 tag with supplied information</summary>
    /// <param name="tag">ID3 data</param>
    private void ApplyID3Tag(ID3TagData tag)
    {
        if (tag == null)
            return;

        _lame.ID3Init();

        // Apply standard ID3 fields
        if (!string.IsNullOrEmpty(tag.Title))
            _lame.ID3SetTitle(tag.Title);
        if (!string.IsNullOrEmpty(tag.Artist))
            _lame.ID3SetArtist(tag.Artist);
        if (!string.IsNullOrEmpty(tag.Album))
            _lame.ID3SetAlbum(tag.Album);
        if (!string.IsNullOrEmpty(tag.Year))
            _lame.ID3SetYear(tag.Year);
        if (!string.IsNullOrEmpty(tag.Comment))
            _lame.ID3SetComment(tag.Comment);
        if (!string.IsNullOrEmpty(tag.Genre))
            _lame.ID3SetGenre(tag.Genre);
        if (!string.IsNullOrEmpty(tag.Track))
            _lame.ID3SetTrack(tag.Track);

        // Apply standard ID3 fields that are not directly supported by LAME
        if (!string.IsNullOrEmpty(tag.Subtitle))
            _lame.ID3SetFieldValue($"TIT3={tag.Subtitle}");
        if (!string.IsNullOrEmpty(tag.AlbumArtist))
            _lame.ID3SetFieldValue($"TPE2={tag.AlbumArtist}");

        // Add user-defined tags if present
        foreach (var kv in tag.UserDefinedText) _lame.ID3SetFieldValue($"TXXX={kv.Key}={kv.Value}");
        // Set the album art if supplied
        if (tag.AlbumArt?.Length > 0)
            _lame.ID3SetAlbumArt(tag.AlbumArt);

        // check size of ID3 tag, if too large write it ourselves.
        var data = _lame.ID3GetID3v2Tag();
        if (data?.Length >= 32768)
        {
            _lame.ID3WriteTagAutomatic = false;

            _outStream.Write(data, 0, data.Length);
        }
    }

    /// <summary>
    ///     Get the bytes of the ID3v1 tag written to the file
    /// </summary>
    /// <returns>Byte array with ID3v1 tag data if available, else null</returns>
    public byte[] GetID3v1TagBytes()
    {
        return _lame.ID3GetID3v1Tag();
    }

    /// <summary>
    ///     Get the bytes of the ID3v2 tag written to the file
    /// </summary>
    /// <returns>Byte array with ID3v2 tag data if supplied, else null</returns>
    public byte[] GetID3v2TagBytes()
    {
        return _lame.ID3GetID3v2Tag();
    }

    private static Dictionary<int, string> _genres;

    /// <summary>Dictionary of Genres supported by LAME's ID3 tag support</summary>
    public static Dictionary<int, string> Genres
    {
        get
        {
            if (_genres == null)
                _genres = LibMp3Lame.ID3GenreList();
            return _genres;
        }
    }

    #endregion

    #region LAME library print hooks

    /// <summary>Set output function for Error output</summary>
    /// <param name="fn">Function to call for Error output</param>
    public void SetErrorFunction(OutputHandler fn)
    {
        _lame.SetErrorFunc(t => fn(t));
    }

    /// <summary>Set output function for Debug output</summary>
    /// <param name="fn">Function to call for Debug output</param>
    public void SetDebugFunction(OutputHandler fn)
    {
        _lame.SetMsgFunc(t => fn(t));
    }

    /// <summary>Set output function for Message output</summary>
    /// <param name="fn">Function to call for Message output</param>
    public void SetMessageFunction(OutputHandler fn)
    {
        _lame.SetMsgFunc(t => fn(t));
    }

    /// <summary>Get configuration of LAME context, results passed to Message function</summary>
    public void PrintLAMEConfig()
    {
        _lame.PrintConfig();
    }

    /// <summary>Get internal settings of LAME context, results passed to Message function</summary>
    public void PrintLAMEInternals()
    {
        _lame.PrintInternals();
    }

    #endregion
}
#pragma warning restore IDE1006 // Naming Styles