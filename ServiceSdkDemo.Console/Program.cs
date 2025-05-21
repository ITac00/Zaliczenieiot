using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using ServiceSdkDemo.SystemConsole;
using ServiceSdkDemo.Lib;

namespace ServiceSdkDemo.SystemConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            string iotConnectionPath = Path.Combine(basePath, "iot_connection.txt");
            string opcuaUrlPath = Path.Combine(basePath, "opcua_url.txt");

            string serviceConnectionString;
            string opcuaUrl;

            // Odczyt connection stringa
            if (File.Exists(iotConnectionPath))
            {
                try
                {
                    serviceConnectionString = File.ReadAllText(iotConnectionPath).Trim();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Main] Błąd odczytu pliku iot_connection.txt: {ex.Message}");
                    serviceConnectionString = ReadAndSaveToFile(iotConnectionPath, "Wpisz connection string do Azure IoT Hub:");
                }
            }
            else
            {
                serviceConnectionString = ReadAndSaveToFile(iotConnectionPath, "Wpisz connection string do Azure IoT Hub:");
            }

            // Odczyt URL-a OPC UA
            if (File.Exists(opcuaUrlPath))
            {
                try
                {
                    opcuaUrl = File.ReadAllText(opcuaUrlPath).Trim();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Main] Błąd odczytu pliku opcua_url.txt: {ex.Message}");
                    opcuaUrl = ReadAndSaveToFile(opcuaUrlPath, "Wpisz URL do serwera OPC UA:");
                }
            }
            else
            {
                opcuaUrl = ReadAndSaveToFile(opcuaUrlPath, "Wpisz URL do serwera OPC UA:");
            }

            using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
            using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

            var manager = new IoTHubManager(serviceClient, registryManager);

            OpcUaManager opcManager;
            try
            {
                opcManager = new OpcUaManager(opcuaUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Main] Nie udało się utworzyć OpcUaManager: {ex.Message}");
                opcManager = null!;
            }

            int input;
            while (true)
            {
                FeatureSelector.PrintMenu();
                input = FeatureSelector.ReadInput();
                if (input == 0)
                    break;

                await FeatureSelector.Execute(input, manager, opcManager);
            }
        }

        static string ReadAndSaveToFile(string filePath, string prompt)
        {
            Console.WriteLine(prompt);
            string input = Console.ReadLine() ?? string.Empty;

            try
            {
                File.WriteAllText(filePath, input);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Main] Nie udało się zapisać pliku {filePath}: {ex.Message}");
            }

            return input;
        }
    }
}
