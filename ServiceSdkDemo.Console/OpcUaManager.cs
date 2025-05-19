using Opc.Ua;
using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceSdkDemo.Lib
{
    public class OpcUaManager : IDisposable
    {
        private readonly OpcClient _client;
        private readonly Dictionary<string, OpcUaDevice> _devices = new();

        public OpcUaManager(string endpointUrl)
        {
            _client = new OpcClient(endpointUrl);
            _client.Connect();
        }

        public void Dispose()
        {
            _client.Disconnect();
        }


        public List<OpcUaDevice> GetDevices()
        {
            try
            {
                // Wymuszone ponowne połączenie — testowa forma
                _client.Disconnect();
                _client.Connect();
                System.Console.WriteLine("[OPC] Ponowne połączenie wykonane przed odczytem urządzeń.");

                List<OpcNodeInfo> deviceNodes;
                try
                {
                    deviceNodes = _client.BrowseNode(OpcObjectTypes.ObjectsFolder).Children().ToList();
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[OPC] Błąd przeglądania przestrzeni nazw: {ex.Message}");
                    return new List<OpcUaDevice>();
                }

                // Filtrujemy urządzenia na podstawie namespace i prefiksu identyfikatora
                deviceNodes = deviceNodes
                    .Where(n => n.NodeId.NamespaceIndex == 2 && n.NodeId.ToString().StartsWith("ns=2;s=Device"))
                    .ToList();

                // Wyczyść wszystkie urządzenia i odczytaj od nowa
                _devices.Clear();

                foreach (var node in deviceNodes)
                {
                    var name = node.DisplayName.Value; // .Value zamiast .ToString()

                    try
                    {
                        var device = new OpcUaDevice(name, node.NodeId, _client);
                        device.Update();
                        _devices[name] = device;
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[OPC] Błąd podczas aktualizacji urządzenia '{name}': {ex.Message}");
                    }
                }

                return _devices.Values.ToList();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[OPC] Błąd przy pobieraniu urządzeń: {ex.Message}");
                return new List<OpcUaDevice>();
            }
        }




    }

    public class OpcUaDevice
    {
        public string Name { get; }
        public string? WorkorderId { get; private set; }
        public string ProductionStatus { get; private set; } = "";
        public int ProductionRate { get; private set; }
        public int GoodCount { get; private set; }
        public int BadCount { get; private set; }
        public float Temperature { get; private set; }

        private readonly OpcNodeId _baseNode;
        private readonly OpcClient _client;

        public OpcUaDevice(string name, OpcNodeId baseNode, OpcClient client)
        {
            Name = name;
            _baseNode = baseNode;
            _client = client;
        }

        public void Update()
        {
            WorkorderId = ReadSafe<string>("WorkorderId");
            ProductionStatus = ReadSafe<string>("ProductionStatus");
            ProductionRate = ReadSafe<int>("ProductionRate");
            GoodCount = ReadSafe<int>("GoodCount");
            BadCount = ReadSafe<int>("BadCount");
            Temperature = ReadSafe<float>("Temperature");
        }

        private T ReadSafe<T>(string subPath)
        {
            try
            {
                // Tworzymy pełny NodeId jako string w znanym formacie
                var nodeId = new OpcNodeId($"{Name}/{subPath}", 2);
                var value = _client.ReadNode(nodeId).Value;
                return (T)Convert.ChangeType(value!, typeof(T));
            }
            catch
            {
                return default!;
            }
        }
    }
}
