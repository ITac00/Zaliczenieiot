using ServiceSdkDemo.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceSdkDemo.SystemConsole
{
    internal static class FeatureSelector
    {
        private static DeviceSimulator? _simulator;
        public static void PrintMenu()
        {
            Console.WriteLine(@"
    1 - Pokaż wszystkie urządzenia
    2 - Pokaż konkretny twin
    3 - Interpretacja flag błędów
    4 - D2C: Start/Stop telemetry
    5 - Utwórz / edytuj pliki konfiguracyjne
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

                case 3:
                    await TryExecuteAsync(PrintErrorFlagInterpretation);
                    break;

                case 4:
                    await TryExecuteAsync(() => D2CMenuAsync(opcUaManager));
                    break;

                case 5:
                    await TryExecuteAsync(ConfigFilesMenuAsync);
                    break;

                default:
                    break;
            }
        }

        private static Task ConfigFilesMenuAsync()
        {
            while (true)
            {
                Console.WriteLine("\nMenu konfiguracji plików:");
                Console.WriteLine("1 - Utwórz/edytuj iot_connection.txt");
                Console.WriteLine("2 - Utwórz/edytuj opcua_url.txt");
                Console.WriteLine("3 - Utwórz/edytuj device_connection.txt");
                Console.WriteLine("0 - Wyjście bez edycji");
                Console.Write("Wybierz opcję: ");

                var input = Console.ReadLine()?.Trim();

                string fileName;
                string prompt;

                switch (input)
                {
                    case "1":
                        fileName = "iot_connection.txt";
                        prompt = "Wpisz connection string do IoT Hub:";
                        break;
                    case "2":
                        fileName = "opcua_url.txt";
                        prompt = "Wpisz URL serwera OPC UA:";
                        break;
                    case "3":
                        fileName = "device_connection.txt";
                        prompt = "Wpisz connection string urządzenia:";
                        break;
                    case "0":
                        Console.WriteLine("Wyjście z menu konfiguracji.");
                        return Task.CompletedTask;
                    default:
                        Console.WriteLine("Nieznana opcja, spróbuj ponownie.");
                        continue;
                }

                Console.WriteLine(prompt);
                var content = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine("Nie wprowadzono żadnej wartości, plik nie zostanie zmieniony.");
                }
                else
                {
                    try
                    {
                        System.IO.File.WriteAllText(fileName, content);
                        Console.WriteLine($"Zapisano do pliku {fileName}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Błąd zapisu pliku {fileName}: {ex.Message}");
                    }
                }

                // Po zapisaniu wracamy do menu lub kończymy - tu możesz zdecydować:
                // Ja proponuję zakończyć po jednej edycji, więc:
                return Task.CompletedTask;
            }
        }


        private static Task D2CMenuAsync(OpcUaManager opcUaManager)
        {
            const string deviceConnectionString = "wstaw_tutaj_primary_connection_string_z_device_identity"; // <- WSTAW

            if (_simulator == null)
                _simulator = new DeviceSimulator(deviceConnectionString, opcUaManager);

            Console.WriteLine("\n1 - Start telemetry");
            Console.WriteLine("2 - Stop telemetry");
            Console.Write("Wybór: ");
            var input = Console.ReadLine()?.Trim();

            switch (input)
            {
                case "1":
                    _simulator.Start();
                    break;
                case "2":
                    _simulator.Stop();
                    break;
                default:
                    Console.WriteLine("Nieznana opcja.");
                    break;
            }

            return Task.CompletedTask;
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
            if (!opcUaManager.EnsureConnected())
            {
                Console.WriteLine("[!] Nie udało się połączyć z serwerem OPC UA.");
                return Task.CompletedTask;
            }

            var devices = opcUaManager.GetDevices();
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
            if (!opcUaManager.EnsureConnected())
            {
                Console.WriteLine("[!] Nie udało się połączyć z serwerem OPC UA.");
                return;
            }

            var devices = opcUaManager.GetDevices();
            if (devices.Count == 0)
            {
                Console.WriteLine("Brak urządzeń lub błąd połączenia.");
                return;
            }

            Console.WriteLine("\n\nWybierz urządzenie:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1} - Reported {devices[i].Name} Twin");
            }

            Console.Write("Twój wybór: ");
            var input = Console.ReadLine();

            if (!int.TryParse(input, out int selectedIndex) || selectedIndex < 1 || selectedIndex > devices.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór.");
                return;
            }

            var selectedDevice = devices[selectedIndex - 1];
            await DeviceActionMenuAsync(selectedDevice, opcUaManager);
        }

        private static Task DeviceActionMenuAsync(OpcUaDevice device, OpcUaManager opcUaManager)
        {
            while (true)
            {
                Console.WriteLine($"\nUrządzenie: {device.Name}");
                Console.WriteLine("1 - Pokaż dane");
                Console.WriteLine("2 - Desired Production Rate");
                Console.WriteLine("3 - Emergency Stop");
                Console.WriteLine("4 - Reset Error Status");
                Console.WriteLine("0 - Powrót");
                Console.Write("Wybierz opcję: ");

                var input = Console.ReadLine();

                if (!opcUaManager.EnsureConnected())
                {
                    Console.WriteLine("[!] Utracono połączenie z serwerem OPC UA.");
                    break;
                }
                var currentDevices = opcUaManager.GetDevices();
                if (!currentDevices.Any(d => d.Name == device.Name))
                {
                    Console.WriteLine($"[!] Urządzenie '{device.Name}' zostało usunięte lub niedostępne.");
                    break;
                }

                switch (input)
                {
                    case "1":
                        try
                        {
                            device.Update();
                            PrintDevice(device);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[!] Nie udało się pobrać danych urządzenia: {ex.Message}");
                            break;
                        }
                        break;

                    case "2":
                        Console.Write("Podaj nowy Production Rate (0-100, co 10): ");
                        var rateStr = Console.ReadLine();
                        if (int.TryParse(rateStr, out int rate))
                        {
                            if (!opcUaManager.EnsureConnected() || !opcUaManager.GetDevices().Any(d => d.Name == device.Name))
                            {
                                Console.WriteLine($"[!] Urządzenie '{device.Name}' zostało usunięte lub niedostępne.");
                                return Task.CompletedTask;
                            }
                            try
                            {
                                if (device.SetProductionRate(rate))
                                {
                                    Console.WriteLine("Production Rate zaktualizowany.");
                                }
                                else
                                {
                                    Console.WriteLine("Nieprawidłowa wartość lub błąd zapisu.");
                                }
                            }
                            catch (Opc.UaFx.OpcException ex)
                            {
                                Console.WriteLine($"[!] Urządzenie utracone lub niedostępne: {ex.Message}");
                                return Task.CompletedTask;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[!] Błąd podczas ustawiania Production Rate: {ex.Message}");
                                return Task.CompletedTask;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Nieprawidłowa wartość.");
                        }
                        break;



                    case "3":
                        try
                        {
                            if (device.SetEmergencyStop(true))
                                Console.WriteLine("Emergency Stop aktywowany.");
                            else
                                Console.WriteLine("Błąd podczas aktywacji Emergency Stop.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[!] Wyjątek przy Emergency Stop: {ex.Message}");
                        }
                        break;

                    case "4":
                        try
                        {
                            if (device.SetEmergencyStop(false))
                                Console.WriteLine("Status błędu zresetowany.");
                            else
                                Console.WriteLine("Błąd podczas resetowania błędu.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[!] Wyjątek przy resetowaniu błędu: {ex.Message}");
                        }
                        break;

                    case "0":
                        return Task.CompletedTask;

                    default:
                        Console.WriteLine("Nieprawidłowy wybór.");
                        break;
                }
            }

            return Task.CompletedTask;
        }

        private static void PrintDevice(OpcUaDevice d)
        {
            try
            {
                d.Update();

                Console.WriteLine($"\nUrządzenie: {d.Name}");
                Console.WriteLine($"  Status: {d.ProductionStatus}");
                Console.WriteLine($"  Workorder ID: {d.WorkorderId}");
                Console.WriteLine($"  Rate: {d.ProductionRate}");
                Console.WriteLine($"  Temp: {d.Temperature}°C");
                Console.WriteLine($"  Good: {d.GoodCount}, Bad: {d.BadCount}");
                Console.WriteLine($"  Device Errors: {d.DeviceErrors} (binary: {Convert.ToString(d.DeviceErrors, 2).PadLeft(4, '0')})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Print] Błąd wypisywania urządzenia '{d.Name}': {ex.Message}");
            }
        }

        private static Task PrintErrorFlagInterpretation()
        {
            Console.WriteLine("\nInterpretacja flag błędów:");
            Console.WriteLine("0000 - Brak błędów");
            Console.WriteLine("0001 - Emergency Stop");
            Console.WriteLine("0010 - Power Failure");
            Console.WriteLine("0100 - Sensor Failure");
            Console.WriteLine("1000 - Unknown Error");
            Console.WriteLine("\nKombinacje flag wskazują na jednoczesne wystąpienie kilku błędów.");
            return Task.CompletedTask;
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