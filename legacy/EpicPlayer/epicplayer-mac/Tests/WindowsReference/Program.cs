// Проверка эталонов на стороне Windows.
//
// Задача: убедиться, что значения в protocol-vectors.json совпадают
// с тем, что реально отправляет существующая реализация. Только после
// этого эталон можно считать источником истины для Swift-версии.
//
// Запуск на Windows:
//     dotnet run --project Tests\WindowsReference -- Tests\Fixtures\protocol-vectors.json
//
// Копировать в репозиторий Windows-версии (EpicPlayer/) не нужно —
// файл достаточно запустить рядом с исходниками.
//
// Целевой фреймворк — 4.5, как у основного проекта.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace WindowsReference
{
    class Program
    {
        // Байты взяты из существующего кода:
        //   MainWindow.xaml.cs:379-387
        //   WifiDevicesWindow.xaml.cs:37
        //   WifiTcpDevicesWindow.xaml.cs:66-70
        static readonly byte[] PlayBytes = { 0x50, 0x05, 0x02, 0, 0, 0, 0, 0x0d };
        static readonly byte[] PauseBytes = { 0x50, 0x05, 0x03, 0, 0, 0, 0x0d };
        static readonly byte[] StopBytes = { 0x50, 0x05, 0x04, 0, 0, 0, 0, 0x0d };
        static readonly byte[] NextBytes = { 0x50, 0x05, 0x05, 0, 0, 0, 0, 0x0d };
        static readonly byte[] PreviousBytes = { 0x50, 0x05, 0x06, 0, 0, 0, 0, 0x0d };
        static readonly byte[] UdpQuery = { 0x49, 0x00, 0x0d };

        static byte[] BuildCommand(byte[] template, uint positionMilliseconds)
        {
            // Ровно то, что делает исходный код:
            // BitConverter.GetBytes((UInt32)...).CopyTo(cmd, 3);
            var result = (byte[])template.Clone();
            BitConverter.GetBytes(positionMilliseconds).CopyTo(result, 3);
            return result;
        }

        static byte[] BuildTcpAnnouncement(IPAddress address)
        {
            // Ровно то, что делает WifiTcpDevicesWindow.xaml.cs:66-70
            var list = new List<byte> { 0x4c, 0x06 };
            list.AddRange(address.GetAddressBytes());
            list.AddRange(BitConverter.GetBytes((ushort)3331));
            list.Add(0x0d);
            return list.ToArray();
        }

        static string ToHex(byte[] bytes)
        {
            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

        static int failures = 0;
        static int checks = 0;

        static void Check(string name, byte[] actual, string expected)
        {
            checks++;
            var actualHex = ToHex(actual);
            if (actualHex != expected)
            {
                failures++;
                Console.WriteLine("FAIL  " + name);
                Console.WriteLine("      ожидалось: " + expected);
                Console.WriteLine("      получено:  " + actualHex);
            }
            else
            {
                Console.WriteLine("ok    " + name);
            }
        }

        // JavaScriptSerializer возвращает Dictionary<string, object> и
        // object[] — такой разбор не зависит от версии .NET.
        static IDictionary<string, object> AsObject(object value)
        {
            return (IDictionary<string, object>)value;
        }

        static IEnumerable<object> AsArray(object value)
        {
            return (IEnumerable)value;
        }

        static string Str(IDictionary<string, object> obj, string key)
        {
            return (string)obj[key];
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Укажите путь к protocol-vectors.json");
                return 2;
            }

            var serializer = new JavaScriptSerializer();
            IDictionary<string, object> root;

            try
            {
                root = AsObject(serializer.DeserializeObject(File.ReadAllText(args[0])));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось прочитать эталон: " + ex.Message);
                return 2;
            }

            // Команды воспроизведения
            foreach (object entry in AsArray(root["playbackCommand"]))
            {
                var vector = AsObject(entry);
                string name = Str(vector, "name");
                string command = Str(vector, "command");
                uint position = Convert.ToUInt32(vector["positionMilliseconds"]);
                string expected = Str(vector, "expected");

                byte[] template;
                switch (command)
                {
                    case "play": template = PlayBytes; break;
                    case "pause": template = PauseBytes; break;
                    case "stop": template = StopBytes; break;
                    case "nextTrack": template = NextBytes; break;
                    case "previousTrack": template = PreviousBytes; break;
                    default:
                        Console.WriteLine("FAIL  неизвестная команда: " + command);
                        failures++;
                        continue;
                }

                Check(name, BuildCommand(template, position), expected);
            }

            // UDP-запрос обнаружения
            foreach (object entry in AsArray(root["udpQuery"]))
            {
                var vector = AsObject(entry);
                Check(Str(vector, "name"), UdpQuery, Str(vector, "expected"));
            }

            // TCP-объявление
            foreach (object entry in AsArray(root["tcpAnnouncement"]))
            {
                var vector = AsObject(entry);
                string address = Str(vector, "address");
                Check(
                    Str(vector, "name"),
                    BuildTcpAnnouncement(IPAddress.Parse(address)),
                    Str(vector, "expected"));
            }

            // Порядок байтов на этой машине. Если проверка провалилась,
            // эталон нужно пересобрать под little endian явно.
            Console.WriteLine();
            Console.WriteLine("Проверка порядка байтов: BitConverter.IsLittleEndian = " +
                              BitConverter.IsLittleEndian);
            if (!BitConverter.IsLittleEndian)
            {
                Console.WriteLine("ВНИМАНИЕ: машина big endian, эталон неприменим.");
            }

            Console.WriteLine();
            Console.WriteLine("Проверок: " + checks + ", несовпадений: " + failures);

            if (failures == 0)
            {
                Console.WriteLine("Эталон подтверждён реализацией Windows.");
                Console.WriteLine("Можно использовать как источник истины для Swift.");
            }
            else
            {
                Console.WriteLine("Эталон НЕ подтверждён. Значения в protocol-vectors.json");
                Console.WriteLine("нужно исправить по столбцу «получено» выше.");
            }

            return failures == 0 ? 0 : 1;
        }
    }
}