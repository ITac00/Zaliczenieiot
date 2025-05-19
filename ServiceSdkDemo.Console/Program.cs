using Microsoft.Azure.Devices;
using ServiceSdkDemo.Console;
using ServiceSdkDemo.Lib;

string serviceConnectionString = "HostName=Zaliczenie.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=Y3seimezGWaCt4rG/NBxoqwdvf2UP90KQAIoTHlyUNQ=";

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

var manager = new IoTHubManager(serviceClient, registryManager);

int input;
do
{
    FeatureSelector.PrintMenu();
    input = FeatureSelector.ReadInput();
    await FeatureSelector.Execute(input, manager);
} while (input != 0);