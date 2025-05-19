using Microsoft.Azure.Devices.Common.Exceptions;
using ServiceSdkDemo.Lib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceSdkDemo.SystemConsole
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
                    System.Console.WriteLine("Type your message:");
                    string messageText = System.Console.ReadLine() ?? string.Empty;

                    System.Console.WriteLine("Type device ID:");
                    string deviceId = System.Console.ReadLine() ?? string.Empty;

                    await manager.SendMessage(messageText, deviceId);
                    System.Console.WriteLine("Message sent!");
                    break;

                case 2:
                    System.Console.WriteLine("Type device ID:");
                    string methodDeviceId = System.Console.ReadLine() ?? string.Empty;
                    try
                    {
                        var result = await manager.ExecuteDeviceMethod("SendMessages", methodDeviceId);
                        System.Console.WriteLine($"Method executed with status {result}");
                    }
                    catch (DeviceNotFoundException)
                    {
                        System.Console.WriteLine("Device not connected!");
                    }
                    break;

                case 3:
                    System.Console.WriteLine("Type desired property name:");
                    string propertyName = System.Console.ReadLine() ?? string.Empty;

                    System.Console.WriteLine("Type device ID:");
                    string twinDeviceId = System.Console.ReadLine() ?? string.Empty;

                    var random = new Random();
                    await manager.UpdateDesiredTwin(twinDeviceId, propertyName, random.Next());
                    System.Console.WriteLine("Desired twin updated.");
                    break;

                case 4:
                    try
                    {
                        var devices = opcUaManager?.GetDevices() ?? new List<Lib.OpcUaDevice>();
                        if (devices.Count == 0)
                        {
                            System.Console.WriteLine("No devices found or unable to connect.");
                            break;
                        }

                        foreach (var d in devices)
                        {
                            try
                            {
                                System.Console.WriteLine($"\nDevice: {d.Name}");
                                System.Console.WriteLine($"  Status: {d.ProductionStatus}");
                                System.Console.WriteLine($"  Workorder ID: {d.WorkorderId}");
                                System.Console.WriteLine($"  Rate: {d.ProductionRate}");
                                System.Console.WriteLine($"  Temp: {d.Temperature}°C");
                                System.Console.WriteLine($"  Good: {d.GoodCount}, Bad: {d.BadCount}");
                            }
                            catch (Exception ex)
                            {
                                System.Console.WriteLine($"[Print] Błąd wypisywania urządzenia '{d.Name}': {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[OPC] Nie udało się pobrać urządzeń: {ex.Message}");
                    }
                    break;

                case 5:
                    try
                    {
                        var devicesToSend = opcUaManager?.GetDevices() ?? new List<Lib.OpcUaDevice>();
                        foreach (var device in devicesToSend)
                        {
                            if (!string.IsNullOrEmpty(device.WorkorderId))
                            {
                                var message = $"Workorder {device.WorkorderId}, Rate {device.ProductionRate}, Temp {device.Temperature}";
                                await manager.SendMessage(message, device.Name);
                                System.Console.WriteLine($"Sent message to {device.Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[OPC->IoT] Błąd podczas wysyłki: {ex.Message}");
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
