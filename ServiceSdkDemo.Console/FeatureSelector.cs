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
                    await TryExecuteAsync(() => ShowSelectedDeviceMenu(opcUaManager));
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

        private static async Task ShowSelectedDeviceMenu(OpcUaManager opcUaManager)
        {
            var devices = opcUaManager?.GetDevices() ?? new List<OpcUaDevice>();
            if (devices.Count == 0)
            {
                Console.WriteLine("Brak urządzeń lub błąd połączenia.");
                return;
            }

            Console.WriteLine("\n\nWybierz urządzenie:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1} - {devices[i].Name}");
            }

            Console.Write("Twój wybór: ");
            var input = Console.ReadLine();

            if (!int.TryParse(input, out int selectedIndex) ||
                selectedIndex < 1 || selectedIndex > devices.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór.");
                return;
            }

            var selectedDevice = devices[selectedIndex - 1];
            await DeviceActionMenu(selectedDevice);
        }

        private static Task DeviceActionMenu(OpcUaDevice device)
        {
            while (true)
            {
                Console.WriteLine($"\nUrządzenie: {device.Name}");
                Console.WriteLine("1 - Pokaż dane");
                Console.WriteLine("2 - Zmień Production Rate");
                Console.WriteLine("3 - Emergency Stop");
                Console.WriteLine("4 - Reset Error Status");
                Console.WriteLine("0 - Powrót");
                Console.Write("Wybierz opcję: ");

                var input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        device.Update();
                        PrintDevice(device);
                        break;
                    case "2":
                        Console.Write("Podaj nowy Production Rate (0-100, co 10): ");
                        var rateStr = Console.ReadLine();
                        if (int.TryParse(rateStr, out int rate) && device.SetProductionRate(rate))
                        {
                            Console.WriteLine("Production Rate zaktualizowany.");
                        }
                        else
                        {
                            Console.WriteLine("Nieprawidłowa wartość lub błąd zapisu.");
                        }
                        break;
                    case "3":
                        if (device.SetEmergencyStop(true))
                            Console.WriteLine("Emergency Stop aktywowany.");
                        else
                            Console.WriteLine("Błąd podczas aktywacji Emergency Stop.");
                        break;
                    case "4":
                        if (device.SetEmergencyStop(false))
                            Console.WriteLine("Status błędu zresetowany.");
                        else
                            Console.WriteLine("Błąd podczas resetowania błędu.");
                        break;
                    case "0":
                        return Task.CompletedTask;
                    default:
                        Console.WriteLine("Nieprawidłowy wybór.");
                        break;
                }
            }
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
