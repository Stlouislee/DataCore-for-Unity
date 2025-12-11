using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NumSharp;

namespace DataCore.Serialization
{
    /// <summary>
    /// NumPy (.npy) format serializer for NDArray
    /// </summary>
    public class NumpySerializer : ISerializer<NDArray>
    {
        private readonly SerializerConfig _config;
        
        public NumpySerializer(SerializerConfig config = null)
        {
            _config = config ?? new SerializerConfig { Format = SerializationFormat.Numpy };
        }
        
        public byte[] Serialize(NDArray data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            using (var stream = new MemoryStream())
            {
                WriteNpyHeader(stream, data);
                WriteNpyData(stream, data);
                return stream.ToArray();
            }
        }
        
        public NDArray Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes cannot be null or empty", nameof(bytes));
            
            using (var stream = new MemoryStream(bytes))
            {
                return ReadNpy(stream);
            }
        }
        
        public async Task SerializeAsync(NDArray data, string filePath, CancellationToken cancellationToken = default)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            var bytes = Serialize(data);
            await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
        }
        
        public async Task<NDArray> DeserializeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            return Deserialize(bytes);
        }
        
        private void WriteNpyHeader(Stream stream, NDArray data)
        {
            var dtype = GetDtypeString(data.dtype);
            var shape = string.Join(",", data.shape);
            var fortranOrder = "False";
            
            var header = $"{{'descr': '{dtype}', 'fortran_order': {fortranOrder}, 'shape': ({shape}), }}";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            
            // Calculate padding to align to 64 bytes
            var headerLength = headerBytes.Length + 10; // 10 bytes for magic and version
            var padding = (int)(64 - (headerLength % 64));
            if (padding < 10) padding += 64;
            
            var paddedHeader = header.PadRight(header.Length + padding, ' ');
            headerBytes = Encoding.ASCII.GetBytes(paddedHeader);
            
            // Write magic string and version
            stream.WriteByte(0x93);
            stream.WriteByte((byte)'N');
            stream.WriteByte((byte)'U');
            stream.WriteByte((byte)'M');
            stream.WriteByte((byte)'P');
            stream.WriteByte((byte)'Y');
            stream.WriteByte(0x01); // Major version
            stream.WriteByte(0x00); // Minor version
            
            // Write header length (little endian)
            var headerLengthBytes = BitConverter.GetBytes((short)headerBytes.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(headerLengthBytes);
            stream.Write(headerLengthBytes, 0, 2);
            
            // Write header
            stream.Write(headerBytes, 0, headerBytes.Length);
        }
        
        private void WriteNpyData(Stream stream, NDArray data)
        {
            var bytes = data.ToByteArray();
            stream.Write(bytes, 0, bytes.Length);
        }
        
        private NDArray ReadNpy(Stream stream)
        {
            // Read magic string
            var magic = new byte[6];
            stream.Read(magic, 0, 6);
            
            if (magic[0] != 0x93 || Encoding.ASCII.GetString(magic, 1, 5) != "NUMPY")
                throw new InvalidDataException("Invalid NumPy file format");
            
            // Read version
            var majorVersion = stream.ReadByte();
            var minorVersion = stream.ReadByte();
            
            // Read header length
            var headerLengthBytes = new byte[2];
            stream.Read(headerLengthBytes, 0, 2);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(headerLengthBytes);
            var headerLength = BitConverter.ToInt16(headerLengthBytes, 0);
            
            // Read header
            var headerBytes = new byte[headerLength];
            stream.Read(headerBytes, 0, headerLength);
            var header = Encoding.ASCII.GetString(headerBytes).TrimEnd('\0', ' ');
            
            // Parse header
            var dtype = ParseDtypeFromHeader(header);
            var shape = ParseShapeFromHeader(header);
            
            // Read data
            var dataSize = CalculateDataSize(shape, dtype);
            var dataBytes = new byte[dataSize];
            var bytesRead = stream.Read(dataBytes, 0, dataSize);
            
            if (bytesRead != dataSize)
                throw new InvalidDataException("Incomplete data in file");
            
            return NDArray.FromByteArray(dataBytes, shape, dtype);
        }
        
        private string GetDtypeString(Type type)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Single => "<f4",
                TypeCode.Double => "<f8",
                TypeCode.Int32 => "<i4",
                TypeCode.Int64 => "<i8",
                TypeCode.Int16 => "<i2",
                TypeCode.UInt32 => "<u4",
                TypeCode.UInt64 => "<u8",
                TypeCode.UInt16 => "<u2",
                TypeCode.Byte => "<u1",
                TypeCode.SByte => "<i1",
                TypeCode.Boolean => "|b1",
                _ => throw new NotSupportedException($"Type {type.Name} is not supported")
            };
        }
        
        private Type ParseDtypeFromHeader(string header)
        {
            var dtypeStart = header.IndexOf("'descr': '") + 10;
            var dtypeEnd = header.IndexOf("'", dtypeStart);
            var dtypeStr = header.Substring(dtypeStart, dtypeEnd - dtypeStart);
            
            return dtypeStr switch
            {
                "<f4" => typeof(float),
                "<f8" => typeof(double),
                "<i4" => typeof(int),
                "<i8" => typeof(long),
                "<i2" => typeof(short),
                "<u4" => typeof(uint),
                "<u8" => typeof(ulong),
                "<u2" => typeof(ushort),
                "<u1" => typeof(byte),
                "<i1" => typeof(sbyte),
                "|b1" => typeof(bool),
                _ => throw new NotSupportedException($"Data type {dtypeStr} is not supported")
            };
        }
        
        private int[] ParseShapeFromHeader(string header)
        {
            var shapeStart = header.IndexOf("'shape': (") + 10;
            var shapeEnd = header.IndexOf(")", shapeStart);
            var shapeStr = header.Substring(shapeStart, shapeEnd - shapeStart);
            
            if (string.IsNullOrWhiteSpace(shapeStr))
                return new int[0];
                
            return shapeStr.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(int.Parse).ToArray();
        }
        
        private int CalculateDataSize(int[] shape, Type dtype)
        {
            var elementSize = dtype.Name switch
            {
                "Single" => 4,
                "Double" => 8,
                "Int32" => 4,
                "Int64" => 8,
                "Int16" => 2,
                "UInt32" => 4,
                "UInt64" => 8,
                "UInt16" => 2,
                "Byte" => 1,
                "SByte" => 1,
                "Boolean" => 1,
                _ => throw new NotSupportedException($"Type {dtype.Name} is not supported")
            };
            
            return shape.Aggregate(1, (a, b) => a * b) * elementSize;
        }
    }
    
    /// <summary>
    /// Extensions for NDArray serialization
    /// </summary>
    public static class NDArrayExtensions
    {
        /// <summary>
        /// Convert NDArray to byte array
        /// </summary>
        public static byte[] ToByteArray(this NDArray array)
        {
            // This is a simplified implementation
            // In a real implementation, you would use NumSharp's internal buffer
            var bytes = new byte[array.size * GetElementSize(array.dtype)];
            Buffer.BlockCopy(array.Array, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        
        /// <summary>
        /// Create NDArray from byte array
        /// </summary>
        public static NDArray FromByteArray(byte[] bytes, int[] shape, Type dtype)
        {
            var array = Array.CreateInstance(dtype, shape);
            Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
            return new NDArray(array);
        }
        
        private static int GetElementSize(Type dtype)
        {
            return Type.GetTypeCode(dtype) switch
            {
                TypeCode.Single => 4,
                TypeCode.Double => 8,
                TypeCode.Int32 => 4,
                TypeCode.Int64 => 8,
                TypeCode.Int16 => 2,
                TypeCode.UInt32 => 4,
                TypeCode.UInt64 => 8,
                TypeCode.UInt16 => 2,
                TypeCode.Byte => 1,
                TypeCode.SByte => 1,
                TypeCode.Boolean => 1,
                _ => throw new NotSupportedException($"Type {dtype.Name} is not supported")
            };
        }
    }
}