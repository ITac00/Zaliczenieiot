using Microsoft.Azure.Devices;
using ServiceSdkDemo.SystemConsole;
using ServiceSdkDemo.Lib;
using System;
using System.Threading.Tasks;

namespace ServiceSdkDemo.SystemConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string serviceConnectionString =
                Environment.GetEnvironmentVariable("IOTHUB_CS") ??
                "HostName=Zaliczenie.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=Y3seimezGWaCt4rG/NBxoqwdvf2UP90KQAIoTHlyUNQ=";

            using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
            using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

            var manager = new IoTHubManager(serviceClient, registryManager);

            OpcUaManager opcManager;
            try
            {
                opcManager = new OpcUaManager("opc.tcp://localhost:4840/");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[Main] Nie udało się utworzyć OpcUaManager: {ex.Message}");
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
    }
}