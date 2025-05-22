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
        private Task? _sendTask;

        private readonly Dictionary<string, int> _lastErrorState = new();

        public DeviceSimulator(string dummy, OpcUaManager opcManager) // "dummy" ignorowany
        {
            _opcManager = opcManager;
        }

        public void Start()
        {
            if (_sendTask != null && !_sendTask.IsCompleted)
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
            _cts = new CancellationTokenSource();

            _sendTask = Task.Run(() => SendLoop(_cts.Token));
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

        private async Task SendLoop(CancellationToken token)
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
                var currentDeviceNames = new HashSet<string>(devices.Select(d => d.Name));

                // Usuń z mapy urządzenia, które zniknęły
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

                        // Wysyłanie standardowej telemetrii
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

                        // Obsługa błędów
                        var currentErrors = device.DeviceErrors;

                        if (!_lastErrorState.ContainsKey(device.Name))
                        {
                            _lastErrorState[device.Name] = 0; // Domyślnie zakładamy 0 jako stan początkowy
                            Console.WriteLine($"[D2C] Zarejestrowano nowe urządzenie: {device.Name}");
                        }

                        if (_lastErrorState[device.Name] != currentErrors)
                        {
                            var errorPayload = new
                            {
                                DeviceName = device.Name,
                                DeviceErrors = currentErrors
                            };

                            await SendMessageAsync(errorPayload, token);
                            Console.WriteLine($"[D2C] [ZMIANA BŁĘDU] {device.Name}: {Convert.ToString(_lastErrorState[device.Name], 2).PadLeft(4, '0')} → {Convert.ToString(currentErrors, 2).PadLeft(4, '0')}");
                            _lastErrorState[device.Name] = currentErrors;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[D2C] Błąd przy urządzeniu '{device.Name}': {ex.Message}");
                    }
                }

                await Task.Delay(5000, token);
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
    }
}
