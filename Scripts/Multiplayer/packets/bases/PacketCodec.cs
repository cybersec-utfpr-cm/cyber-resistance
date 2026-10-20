using System;
using System.Buffers.Binary;
using System.Text;

/// Utilitarios de serializacao dos pacotes (lobby e in-game).
/// No Godot 4.4 C# o PackedByteArray do GDScript vira byte[], entao os helpers
/// encode_u8/decode_u8/encode_float/... sao reproduzidos aqui com
/// BinaryPrimitives (little-endian, mesmo byte order do Godot) e Encoding.UTF8.
/// Qualquer mudanca aqui quebra a compatibilidade com o cliente standalone (GDScript).
public static class PacketCodec
{
	public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text ?? string.Empty);

	public static string FromUtf8(byte[] data, int offset, int length) =>
		Encoding.UTF8.GetString(data, offset, length);

	public static string FromUtf8(byte[] data, int offset) =>
		Encoding.UTF8.GetString(data, offset, data.Length - offset);

	public static void EncodeU8(byte[] buffer, int offset, long value) =>
		buffer[offset] = (byte)value;

	public static long DecodeU8(byte[] data, int offset) => data[offset];

	public static void EncodeU16(byte[] buffer, int offset, long value) =>
		BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), (ushort)value);

	public static long DecodeU16(byte[] data, int offset) =>
		BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));

	public static void EncodeU32(byte[] buffer, int offset, long value) =>
		BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), (uint)value);

	public static long DecodeU32(byte[] data, int offset) =>
		BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));

	public static void EncodeFloat(byte[] buffer, int offset, float value) =>
		BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(offset), value);

	public static float DecodeFloat(byte[] data, int offset) =>
		BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset));

	/// Equivalente a PackedByteArray.slice(begin, end).
	public static byte[] Slice(byte[] data, int begin, int end) => data[begin..end];

	/// Equivalente a PackedByteArray.slice(begin) — do offset ate o fim.
	public static byte[] Slice(byte[] data, int begin) => data[begin..];

	/// Equivalente a PackedByteArray.resize().
	public static byte[] Resize(byte[] data, int size)
	{
		if (data.Length == size)
		{
			return data;
		}
		byte[] result = new byte[size];
		Array.Copy(data, result, Math.Min(data.Length, size));
		return result;
	}
}
