using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceSdkDemo.Lib
{
    public class DeviceSimulator
    {
        private readonly OpcUaManager _opcManager;
        private readonly string _connectionStringPath = "device_connection.txt";
        private DeviceClient? _client;
        private CancellationTokenSource? _cts;
        private Task? _sendTelemetryTask;
        private Task? _sendErrorTask;

        private readonly Dictionary<string, int> _lastErrorState = new();

        public DeviceSimulator(string dummy, OpcUaManager opcManager) 
        {
            _opcManager = opcManager;
        }

        public void Start()
        {
            if ((_sendTelemetryTask != null && !_sendTelemetryTask.IsCompleted) ||
                (_sendErrorTask != null && !_sendErrorTask.IsCompleted))
            {
                Console.WriteLine("[D2C] Telemetria już działa.");
                return;
            }

            var connectionString = LoadConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("[D2C] Nie znaleziono pliku z connection stringiem.");
                return;
            }

            _client = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);

            // Rejestracja obsługi metody bezpośredniej ControlDevice
            _client.SetMethodHandlerAsync("ControlDevice", ControlDeviceHandler, null).Wait();

            _cts = new CancellationTokenSource();

            _sendTelemetryTask = Task.Run(() => SendTelemetryLoop(_cts.Token));
            _sendErrorTask = Task.Run(() => SendErrorLoop(_cts.Token));

            Console.WriteLine("[D2C] Telemetria uruchomiona.");
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
                Console.WriteLine("[D2C] Telemetria zatrzymana.");
            }
        }

        private async Task SendTelemetryLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_opcManager.EnsureConnected())
                {
                    Console.WriteLine("[D2C] Brak połączenia z OPC UA.");
                    await Task.Delay(2000, token);
                    continue;
                }

                var devices = _opcManager.GetDevices();

                foreach (var device in devices)
                {
                    try
                    {
                        device.Update();

                        var telemetry = new
                        {
                            DeviceName = device.Name,
                            device.ProductionStatus,
                            device.WorkorderId,
                            device.GoodCount,
                            device.BadCount,
                            device.Temperature
                        };

                        await SendMessageAsync(telemetry, token);
                        Console.WriteLine($"[D2C] Wysłano dane dla: {device.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[D2C] Błąd przy urządzeniu '{device.Name}': {ex.Message}");
                    }
                }

                await Task.Delay(5000, token);
            }
        }

        private async Task SendErrorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_opcManager.EnsureConnected())
                {
                    await Task.Delay(100, token);
                    continue;
                }

                var devices = _opcManager.GetDevices();
                var currentDeviceNames = new HashSet<string>(devices.Select(d => d.Name));

                var removed = _lastErrorState.Keys.Except(currentDeviceNames).ToList();
                foreach (var name in removed)
                {
                    _lastErrorState.Remove(name);
                    Console.WriteLine($"[D2C] Usunięto z mapy stanów: {name}");
                }

                foreach (var device in devices)
                {
                    try
                    {
                        device.Update();

                        var currentErrors = device.DeviceErrors;

                        if (!_lastErrorState.ContainsKey(device.Name))
                        {
                            _lastErrorState[device.Name] = 0;
                            Console.WriteLine($"[D2C] Zarejestrowano nowe urządzenie: {device.Name}");
                        }

                        int previousErrors = _lastErrorState[device.Name];
                        int newlySetErrors = currentErrors & ~previousErrors;

                        if (newlySetErrors > 0)
                        {
                            for (int bit = 0; bit < 32; bit++) // assuming 32-bit error field
                            {
                                int mask = 1 << bit;
                                if ((newlySetErrors & mask) != 0)
                                {
                                    var errorPayload = new
                                    {
                                        DeviceName = device.Name,
                                        ErrorBit = bit,
                                        ErrorCode = mask
                                    };

                                    await SendMessageAsync(errorPayload, token);
                                    Console.WriteLine($"[D2C] [NOWY BŁĄD] {device.Name} → Bit {bit} (0x{mask:X})");
                                }
                            }
                        }

                        _lastErrorState[device.Name] = currentErrors;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[D2C] Błąd przy urządzeniu '{device.Name}': {ex.Message}");
                    }
                }

                await Task.Delay(100, token);
            }
        }

        private async Task SendMessageAsync(object payload, CancellationToken token)
        {
            var json = JsonConvert.SerializeObject(payload);
            var message = new Message(Encoding.UTF8.GetBytes(json))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            await _client!.SendEventAsync(message, token);
        }

        private string LoadConnectionString()
        {
            try
            {
                if (!File.Exists(_connectionStringPath))
                {
                    Console.WriteLine("[D2C] Nie znaleziono pliku z device connection stringiem.");
                    Console.Write("Wprowadź connection string urządzenia: ");
                    var userInput = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(userInput))
                    {
                        Console.WriteLine("[D2C] Nie podano connection stringa. Przerwano.");
                        return string.Empty;
                    }

                    File.WriteAllText(_connectionStringPath, userInput);
                    Console.WriteLine("[D2C] Zapisano connection string do pliku.");
                    return userInput;
                }

                return File.ReadAllText(_connectionStringPath).Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[D2C] Błąd przy wczytywaniu lub zapisie connection stringa: {ex.Message}");
                return string.Empty;
            }
        }


        private async Task<MethodResponse> ControlDeviceHandler(MethodRequest methodRequest, object userContext)
        {
            try
            {
                var json = Encoding.UTF8.GetString(methodRequest.Data);
                var command = JsonConvert.DeserializeObject<DeviceCommand>(json);

                if (command == null || string.IsNullOrWhiteSpace(command.Device) || string.IsNullOrWhiteSpace(command.Command))
                    return await CreateResponseAsync(400, "Invalid payload format.");

                if (!_opcManager.EnsureConnected())
                    return await CreateResponseAsync(500, "OPC UA not connected.");

                bool result = false;

                switch (command.Command)
                {
                    case "EmergencyStop":
                        result = _opcManager.EmergencyStop(command.Device);
                        break;

                    case "ResetErrorStatus":
                        result = _opcManager.ResetErrorStatus(command.Device);
                        break;

                    case "DecreaseProductionRate":
                        result = _opcManager.DecreaseProductionRate(command.Device);
                        break;

                    default:
                        return await CreateResponseAsync(404, $"Unknown command: {command.Command}");
                }

                if (result)
                    return await CreateResponseAsync(200, $"Command '{command.Command}' executed on device '{command.Device}'.");
                else
                    return await CreateResponseAsync(500, $"Failed to execute '{command.Command}' on device '{command.Device}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[D2C] Błąd obsługi metody bezpośredniej: {ex.Message}");
                return await CreateResponseAsync(500, "Internal server error.");
            }
        }


        private Task<MethodResponse> CreateResponseAsync(int status, string message)
        {
            var payload = new { message };
            var json = JsonConvert.SerializeObject(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            return Task.FromResult(new MethodResponse(bytes, status));
        }

        private class DeviceCommand
        {
            public string Device { get; set; } = string.Empty;
            public string Command { get; set; } = string.Empty;
        }
    }
}
