using Microsoft.Azure.Devices;
using ServiceSdkDemo.Console;
using ServiceSdkDemo.Lib;

string serviceConnectionString =
    Environment.GetEnvironmentVariable("IOTHUB_CS") ??
    "HostName=Zaliczenie.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=Y3seimezGWaCt4rG/NBxoqwdvf2UP90KQAIoTHlyUNQ=";

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

// IoTHubManager nie implementuje IDisposable, więc bez 'using'
var manager = new IoTHubManager(serviceClient, registryManager);

// OpcUaManager implementuje IDisposable, więc można go owijać w using
using var opcManager = new OpcUaManager("opc.tcp://localhost:4840/");

int input;
do
{
    FeatureSelector.PrintMenu();
    input = FeatureSelector.ReadInput();
    // Przekazujemy trzy argumenty: wybór, IoTHubManager i OpcUaManager
    await FeatureSelector.Execute(input, manager, opcManager);
}
while (input != 0);
