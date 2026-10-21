// .NET Framework had no BitConverter.GetBytes(Half), so PixelTuner 2's GetBytes(byte) calls
// went to GetBytes(short) and gave two bytes. Today the call is ambiguous; this class, found
// before System.BitConverter inside the namespace, keeps the old meaning.
namespace Communication.Gradient
{
    internal static class BitConverter
    {
        public static byte[] GetBytes(byte value) => System.BitConverter.GetBytes((short)value);
        public static byte[] GetBytes(short value) => System.BitConverter.GetBytes(value);
        public static byte[] GetBytes(ushort value) => System.BitConverter.GetBytes(value);
        public static byte[] GetBytes(int value) => System.BitConverter.GetBytes(value);
        public static byte[] GetBytes(uint value) => System.BitConverter.GetBytes(value);
    }
}
