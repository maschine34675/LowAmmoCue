using System;
using System.IO;
using System.Text;

namespace LowAmmoCue;
internal sealed class PcmWave
{
    internal readonly float[] Samples;
    internal readonly int Rate;
    internal readonly int Channels;
    internal int Frames => Samples.Length / Channels;

    private PcmWave(float[] samples, int rate, int channels)
    { Samples = samples; Rate = rate; Channels = channels; }

    internal static PcmWave Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    internal static PcmWave Read(Stream stream)
    {
        if (stream.Length < 44 || stream.Length > 1200000)
            throw new InvalidDataException("Expected a short PCM WAV, at most 1.2 MB.");
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        string Tag() => Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Tag() != "RIFF" || reader.ReadUInt32() != stream.Length - 8 || Tag() != "WAVE")
            throw new InvalidDataException("Invalid RIFF/WAVE header.");
        int rate = 0, channels = 0, bits = 0, align = 0;
        byte[] pcm = null;
        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < 8) throw new InvalidDataException("Truncated WAV chunk.");
            string tag = Tag();
            uint size = reader.ReadUInt32();
            long end = stream.Position + size;
            long paddedEnd = end + (size & 1);
            if (paddedEnd > stream.Length) throw new InvalidDataException("WAV chunk exceeds file.");
            if (tag == "fmt ")
            {
                if (rate != 0 || size < 16 || reader.ReadUInt16() != 1)
                    throw new InvalidDataException("Expected one uncompressed PCM format chunk.");
                channels = reader.ReadUInt16();
                rate = reader.ReadInt32();
                int byteRate = reader.ReadInt32();
                align = reader.ReadUInt16();
                bits = reader.ReadUInt16();
                if (channels < 1 || channels > 2 || rate < 8000 || rate > 96000
                    || (bits != 16 && bits != 24) || align != channels * (bits / 8) || byteRate != rate * align)
                    throw new InvalidDataException("Use 8-96 kHz mono/stereo PCM16 or PCM24 WAV.");
            }
            else if (tag == "data")
            {
                if (pcm != null || size == 0) throw new InvalidDataException("Invalid or duplicate data chunk.");
                pcm = reader.ReadBytes(checked((int)size));
            }
            stream.Position = paddedEnd;
        }
        if (rate == 0 || pcm == null || pcm.Length % align != 0 || pcm.Length / align > rate * 2)
            throw new InvalidDataException("Missing format/data, incomplete frame, or audio longer than two seconds.");
        int sampleBytes = bits / 8;
        var samples = new float[pcm.Length / sampleBytes];
        float peak = 0;
        for (int i = 0, offset = 0; i < samples.Length; i++, offset += sampleBytes)
        {
            int value = pcm[offset] | (pcm[offset + 1] << 8);
            if (bits == 16) samples[i] = (short)value / 32768f;
            else
            {
                value |= pcm[offset + 2] << 16;
                value = (value << 8) >> 8;
                samples[i] = value / 8388608f;
            }
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }
        if (peak <= 0.00001f) throw new InvalidDataException("WAV contains no audible signal.");
        return new PcmWave(samples, rate, channels);
    }
}
