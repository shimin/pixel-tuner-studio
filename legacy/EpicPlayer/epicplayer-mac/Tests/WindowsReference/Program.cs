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
    // Классы повторяют структуру protocol-vectors.json. Разбор идёт
    // в типизированные объекты, а не приведением object по ходу работы:
    // при несовпадении структуры файла сообщение об ошибке должно
    // называть поле, а не сообщать о невозможности приведения типа.
    class VectorFile
    {
        public List<PlaybackVector> playbackCommand { get; set; }
        public List<UdpVector> udpQuery { get; set; }
        public List<TcpVector> tcpAnnouncement { get; set; }
    }

    class PlaybackVector
    {
        public string name { get; set; }
        public string command { get; set; }
        public uint positionMilliseconds { get; set; }
        public string expected { get; set; }
    }

    class UdpVector
    {
        public string name { get; set; }
        public string expected { get; set; }
    }

    class TcpVector
    {
        public string name { get; set; }
        public string address { get; set; }
        public string expected { get; set; }
    }

    class Program
    {
        // Байты взяты из существующего кода:
        //   MainWindow.xaml.cs:379-387
        //   WifiDevicesWindow.xaml.cs:37
        //   WifiTcpDevicesWindow.xaml.cs:66-70
        static readonly byte[] PlayBytes = { 0x50, 0x05, 0x02, 0, 0, 0, 0, 0x0d };
        static readonly byte[] PauseBytes = { 0x50, 0x05, 0x03, 0, 0, 0, 0, 0x0d };
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

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Укажите путь к protocol-vectors.json");
                return 2;
            }

            VectorFile vectors;
            try
            {
                var serializer = new JavaScriptSerializer();
                vectors = serializer.Deserialize<VectorFile>(File.ReadAllText(args[0]));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось прочитать эталон: " + ex.Message);
                Console.WriteLine("Проверьте путь: " + args[0]);
                return 2;
            }

            if (vectors == null)
            {
                Console.WriteLine("Файл пуст.");
                return 2;
            }
            if (vectors.playbackCommand == null)
            {
                Console.WriteLine("В файле нет раздела playbackCommand.");
                return 2;
            }
            if (vectors.udpQuery == null)
            {
                Console.WriteLine("В файле нет раздела udpQuery.");
                return 2;
            }
            if (vectors.tcpAnnouncement == null)
            {
                Console.WriteLine("В файле нет раздела tcpAnnouncement.");
                return 2;
            }

            // Команды воспроизведения
            foreach (var vector in vectors.playbackCommand)
            {
                byte[] template;
                switch (vector.command)
                {
                    case "play": template = PlayBytes; break;
                    case "pause": template = PauseBytes; break;
                    case "stop": template = StopBytes; break;
                    case "nextTrack": template = NextBytes; break;
                    case "previousTrack": template = PreviousBytes; break;
                    default:
                        Console.WriteLine("FAIL  неизвестная команда: " + vector.command);
                        failures++;
                        continue;
                }

                Check(vector.name, BuildCommand(template, vector.positionMilliseconds), vector.expected);
            }

            // UDP-запрос обнаружения
            foreach (var vector in vectors.udpQuery)
            {
                Check(vector.name, UdpQuery, vector.expected);
            }

            // TCP-объявление
            foreach (var vector in vectors.tcpAnnouncement)
            {
                Check(
                    vector.name,
                    BuildTcpAnnouncement(IPAddress.Parse(vector.address)),
                    vector.expected);
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