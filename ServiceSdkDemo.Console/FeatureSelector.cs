using ServiceSdkDemo.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceSdkDemo.SystemConsole
{
    internal static class FeatureSelector
    {
        public static void PrintMenu()
        {
            Console.WriteLine(@"
    1 - Pokaż wszystkie urządzenia OPC UA
    2 - Pokaż stan wybranego urządzenia
    0 - Wyjście");
        }

        public static async Task Execute(int feature, IoTHubManager manager, OpcUaManager opcUaManager)
        {
            switch (feature)
            {
                case 1:
                    await TryExecuteAsync(() => ShowAllDevices(opcUaManager));
                    break;

                case 2:
                    await TryExecuteAsync(() => ShowSelectedDevice(opcUaManager));
                    break;

                default:
                    break;
            }
        }

        private static async Task TryExecuteAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[!] Wystąpił błąd: {ex.Message}");
                Console.Write("Czy chcesz spróbować ponownie? (T/N): ");
                var retry = Console.ReadLine()?.Trim().ToUpper();
                if (retry == "T")
                {
                    await TryExecuteAsync(action);
                }
            }
        }

        private static Task ShowAllDevices(OpcUaManager opcUaManager)
        {
            var devices = opcUaManager?.GetDevices() ?? new List<OpcUaDevice>();
            if (devices.Count == 0)
            {
                Console.WriteLine("Brak urządzeń lub błąd połączenia.");
                return Task.CompletedTask;
            }

            foreach (var d in devices)
            {
                PrintDevice(d);
            }

            return Task.CompletedTask;
        }

        private static Task ShowSelectedDevice(OpcUaManager opcUaManager)
        {
            var devices = opcUaManager?.GetDevices() ?? new List<OpcUaDevice>();
            if (devices.Count == 0)
            {
                Console.WriteLine("Brak urządzeń lub błąd połączenia.");
                return Task.CompletedTask;
            }

            Console.WriteLine("\n\nWybierz urządzenie:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1} - {devices[i].Name}");
            }

            Console.Write("Twój wybór: ");
            var key = Console.ReadKey(intercept: true);
            Console.WriteLine();

            if (!int.TryParse(key.KeyChar.ToString(), out int selectedIndex) ||
                selectedIndex < 1 || selectedIndex > devices.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór.");
                return Task.CompletedTask;
            }

            var selectedDevice = devices[selectedIndex - 1];
            PrintDevice(selectedDevice);
            return Task.CompletedTask;
        }


        private static void PrintDevice(OpcUaDevice d)
        {
            try
            {
                Console.WriteLine($"\nUrządzenie: {d.Name}");
                Console.WriteLine($"  Status: {d.ProductionStatus}");
                Console.WriteLine($"  Workorder ID: {d.WorkorderId}");
                Console.WriteLine($"  Rate: {d.ProductionRate}");
                Console.WriteLine($"  Temp: {d.Temperature}°C");
                Console.WriteLine($"  Good: {d.GoodCount}, Bad: {d.BadCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Print] Błąd wypisywania urządzenia '{d.Name}': {ex.Message}");
            }
        }

        internal static int ReadInput()
        {
            Console.Write("\nWybierz opcję: ");
            var key = Console.ReadKey(intercept: true);
            Console.WriteLine();
            return int.TryParse(key.KeyChar.ToString(), out int result) ? result : -1;
        }
    }
}
