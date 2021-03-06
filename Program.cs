using System;
using System.IO;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ConsoleApp3
{
    public struct TcpHeader
    {
        public uint SourceIp;
        public uint DestinationIp;
        public ushort SourcePort;
        public ushort DestinationPort;
        public uint Flags;
        public uint Checksum;
    }

    public class MemoryStream2 : Stream
    {
        readonly byte[] _buf;
        long _offset;

        public MemoryStream2(byte[] buf) => _buf = buf;

        public override bool CanRead => true;

        public override long Length => _buf.Length;

        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public unsafe override int Read(byte[] buffer, int offset, int count)
        {
            long avail = _buf.LongLength - _offset;
            if (avail > count)
            {
                avail = count;
            }

            if (avail <= 0)
            {
                return 0;
            }

            fixed (byte* p1 = &_buf[_offset], p2 = &buffer[offset])
            {
                Buffer.MemoryCopy(p1, p2, avail, avail);
            }

            _offset += avail;
            return unchecked((int)avail);
        }

        // nothing to do for this one
        public override void Flush() { }

        // not because we can't, but because I don't feel like it.
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public class BinaryReader2
    {
        readonly Stream _stream;
        readonly byte[] _buf = new byte[4];

        public BinaryReader2(Stream stream) => _stream = stream;

        public virtual uint ReadUInt32()
        {
            _stream.Read(_buf, 0, 4);
            return Unsafe.As<byte, uint>(ref _buf[0]);
        }

        public virtual ushort ReadUInt16()
        {
            _stream.Read(_buf, 0, 2);
            return Unsafe.As<byte, ushort>(ref _buf[0]);
        }
    }

    public sealed class BinaryReader3
    {
        readonly byte[] _buf;
        long _offset;

        public BinaryReader3(byte[] buf) => _buf = buf;

        public uint ReadUInt32() => Unsafe.As<byte, uint>(ref _buf[(_offset += 4) - 4]);

        public ushort ReadUInt16() => Unsafe.As<byte, ushort>(ref _buf[(_offset += 2) - 2]);
    }

    public static class BinaryReader4
    {
        public static uint ReadUInt32(byte[] buf, ref int offset) => Unsafe.As<byte, uint>(ref buf[(offset += 4) - 4]);

        public static ushort ReadUInt16(byte[] buf, ref int offset) => Unsafe.As<byte, ushort>(ref buf[(offset += 2) - 2]);
    }

    public class Bencher
    {
        const int HeaderCount = 40000;

        static readonly int HeaderSize = Unsafe.SizeOf<TcpHeader>();

        static readonly byte[] data = new byte[HeaderCount * HeaderSize];

        static Bencher() => new Random(12345).NextBytes(data);

        [Benchmark]
        public void ReadHeadersBase()
        {
            var ms = new MemoryStream(data);
            var br = new BinaryReader(ms);

            for (int i = 0; i < HeaderCount; i++)
            {
                var header = new TcpHeader();
                header.SourceIp = br.ReadUInt32();
                header.DestinationIp = br.ReadUInt32();
                header.SourcePort = br.ReadUInt16();
                header.DestinationPort = br.ReadUInt16();
                header.Flags = br.ReadUInt32();
                header.Checksum = br.ReadUInt32();
            }
        }

        [Benchmark]
        public void ReadHeadersOptimized_StillVirtual()
        {
            var ms = new MemoryStream2(data);
            var br = new BinaryReader2(ms);

            for (int i = 0; i < HeaderCount; i++)
            {
                var header = new TcpHeader();
                header.SourceIp = br.ReadUInt32();
                header.DestinationIp = br.ReadUInt32();
                header.SourcePort = br.ReadUInt16();
                header.DestinationPort = br.ReadUInt16();
                header.Flags = br.ReadUInt32();
                header.Checksum = br.ReadUInt32();
            }
        }

        [Benchmark]
        public void ReadHeadersOptimized_NonVirtual()
        {
            var br = new BinaryReader3(data);

            for (int i = 0; i < HeaderCount; i++)
            {
                var header = new TcpHeader();
                header.SourceIp = br.ReadUInt32();
                header.DestinationIp = br.ReadUInt32();
                header.SourcePort = br.ReadUInt16();
                header.DestinationPort = br.ReadUInt16();
                header.Flags = br.ReadUInt32();
                header.Checksum = br.ReadUInt32();
            }
        }

        [Benchmark]
        public void ReadHeadersOptimized_NonInstance()
        {
            int offset = 0;
            for (int i = 0; i < HeaderCount; i++)
            {
                var header = new TcpHeader();
                header.SourceIp = BinaryReader4.ReadUInt32(data, ref offset);
                header.DestinationIp = BinaryReader4.ReadUInt32(data, ref offset);
                header.SourcePort = BinaryReader4.ReadUInt16(data, ref offset);
                header.DestinationPort = BinaryReader4.ReadUInt16(data, ref offset);
                header.Flags = BinaryReader4.ReadUInt32(data, ref offset);
                header.Checksum = BinaryReader4.ReadUInt32(data, ref offset);
            }
        }

        [Benchmark]
        public unsafe void ReadHeadersOptimized_ManuallyPumpedUnsafe()
        {
            for (int i = 0; i < data.Length; i += HeaderSize)
            {
                fixed (byte* p = &data[i])
                {
                    var header = *(TcpHeader*)p;
                }
            }
        }

        [Benchmark]
        public void ReadHeadersOptimized_MaximumPower()
        {
            for (int i = 0; i < data.Length; i += HeaderSize)
            {
                var header = Unsafe.As<byte, TcpHeader>(ref data[i]);
            }
        }
    }

    static class Program
    {
        static void Main(string[] args) => BenchmarkRunner.Run<Bencher>();
    }
}
