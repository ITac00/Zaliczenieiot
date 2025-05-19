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

        private void ReconnectIfNeeded()
        {
            try
            {
                _client.ReadNode("ns=0;i=2253"); // "Server" node – zawsze istnieje
            }
            catch
            {
                try
                {
                    System.Console.WriteLine("[OPC] Utracono połączenie. Próba ponownego połączenia...");
                    _client.Connect();
                    System.Console.WriteLine("[OPC] Ponownie połączono z serwerem OPC UA.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"[OPC] Nie można się połączyć z serwerem OPC UA: {ex.Message}");
                }
            }
        }


        public List<OpcUaDevice> GetDevices()
        {
            try
            {
                ReconnectIfNeeded();

                var deviceNodes = _client.BrowseNode(OpcObjectTypes.ObjectsFolder)
                    .Children()
                    .Where(n => n.NodeId.NamespaceIndex == 2 && n.NodeId.ToString().StartsWith("ns=2;s=Device"))
                    .ToList();

                var currentNames = new HashSet<string>(
                    deviceNodes.Select(n => n.DisplayName.ToString())
                );

                foreach (var node in deviceNodes)
                {
                    var name = node.DisplayName.ToString();

                    try
                    {
                        if (!_devices.ContainsKey(name))
                        {
                            _devices[name] = new OpcUaDevice(name, node.NodeId, _client);
                        }

                        _devices[name].Update();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[OPC] Błąd podczas aktualizacji urządzenia '{name}': {ex.Message}");
                    }
                }

                // Usuń nieistniejące
                var removed = _devices.Keys.Except(currentNames).ToList();
                foreach (var name in removed)
                {
                    _devices.Remove(name);
                }

                return _devices.Values.ToList();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[OPC] Błąd przy przeglądaniu urządzeń: {ex.Message}");
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
