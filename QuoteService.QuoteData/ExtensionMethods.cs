using System;
using System.IO;
using Google.Protobuf;

namespace QuoteService.QuoteData
{
    public static class ExtensionMethods
    {
        public static void LogOHLC(this OHLC ohlc)
        {
            var str = $"{ohlc.LocalTime.ToDateTime().ToLocalTime().ToString("HH:mm:ss.ffffff")}: " +
                   $"O: {ohlc.Open.Value.ToString()}, " +
                   $"H: {ohlc.High.Value.ToString()}, " +
                   $"L: {ohlc.Low.Value.ToString()}, " +
                   $"C: {ohlc.Close.ToString()}, " +
                   $"V: {ohlc.Volume.ToString()}, " +
                   $"tc: {ohlc.TicksCount.ToString()}";
            Console.WriteLine(str);
        }
        public static string SerializeToString_PB(this IMessage obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                obj.WriteTo(ms);
                return Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }
    }
}