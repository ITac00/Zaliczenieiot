using Microsoft.Azure.Devices.Common.Exceptions;
using ServiceSdkDemo.Lib;
using System.Threading.Tasks;

namespace ServiceSdkDemo.Console
{
    internal static class FeatureSelector
    {
        public static void PrintMenu()
        {
            System.Console.WriteLine(@"
    1 - C2D (Send message)
    2 - Direct Method (SendMessages)
    3 - Device Twin (update desired property)
    4 - Show OPC UA devices
    5 - Send OPC UA device data to IoT Hub
    0 - Exit");
        }

        public static async Task Execute(int feature, IoTHubManager manager, OpcUaManager opcUaManager)
        {
            switch (feature)
            {
                case 1:
                    {
                        System.Console.WriteLine("Type your message:");
                        string messageText = System.Console.ReadLine() ?? "";

                        System.Console.WriteLine("Type device ID:");
                        string deviceId = System.Console.ReadLine() ?? "";

                        await manager.SendMessage(messageText, deviceId);
                        System.Console.WriteLine("Message sent!");
                    }
                    break;

                case 2:
                    {
                        System.Console.WriteLine("Type device ID:");
                        string deviceId = System.Console.ReadLine() ?? "";
                        try
                        {
                            var result = await manager.ExecuteDeviceMethod("SendMessages", deviceId);
                            System.Console.WriteLine($"Method executed with status {result}");
                        }
                        catch (DeviceNotFoundException)
                        {
                            System.Console.WriteLine("Device not connected!");
                        }
                    }
                    break;

                case 3:
                    {
                        System.Console.WriteLine("Type desired property name:");
                        string propertyName = System.Console.ReadLine() ?? "";

                        System.Console.WriteLine("Type device ID:");
                        string deviceId = System.Console.ReadLine() ?? "";

                        var random = new Random();
                        await manager.UpdateDesiredTwin(deviceId, propertyName, random.Next());
                    }
                    break;

                case 4:
                    {
                        var devices = opcUaManager.GetDevices();
                        if (devices.Count == 0)
                        {
                            System.Console.WriteLine("No devices found.");
                            break;
                        }

                        foreach (var d in devices)
                        {
                            System.Console.WriteLine($"\nDevice: {d.Name}");
                            System.Console.WriteLine($"  Status: {d.ProductionStatus}");
                            System.Console.WriteLine($"  Workorder ID: {d.WorkorderId}");
                            System.Console.WriteLine($"  Rate: {d.ProductionRate}");
                            System.Console.WriteLine($"  Temp: {d.Temperature}°C");
                            System.Console.WriteLine($"  Good: {d.GoodCount}, Bad: {d.BadCount}");
                        }
                    }
                    break;

                case 5:
                    {
                        var devices = opcUaManager.GetDevices();
                        foreach (var device in devices)
                        {
                            if (!string.IsNullOrEmpty(device.WorkorderId))
                            {
                                var message = $"Workorder {device.WorkorderId}, Rate {device.ProductionRate}, Temp {device.Temperature}";
                                await manager.SendMessage(message, device.Name);
                                System.Console.WriteLine($"Sent message to {device.Name}");
                            }
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        internal static int ReadInput()
        {
            var keyPressed = System.Console.ReadKey();
            System.Console.WriteLine();
            return int.TryParse(keyPressed.KeyChar.ToString(), out int result) ? result : -1;
        }
    }
}
