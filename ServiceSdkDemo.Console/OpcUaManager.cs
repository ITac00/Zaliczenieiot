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
        private readonly string _endpointUrl;
        private bool _isConnected = false;
        private readonly Dictionary<string, OpcUaDevice> _devices = new();

        public OpcUaManager(string endpointUrl)
        {
            _endpointUrl = endpointUrl;
            _client = new OpcClient(endpointUrl);
        }

        public void Dispose()
        {
            if (_isConnected)
            {
                _client.Disconnect();
                _isConnected = false;
            }
        }
        public bool EnsureConnected()
        {
            try
            {
                if (!_isConnected)
                {
                    _client.Connect();
                    _isConnected = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OPC] Nie udało się połączyć: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }


        public List<OpcUaDevice> GetDevices()
        {
            try
            {
                if (_isConnected)
                {
                    _client.Disconnect();
                    _isConnected = false;
                }

                _client.Connect();
                _isConnected = true;

                var deviceNodes = _client
                    .BrowseNode(OpcObjectTypes.ObjectsFolder)
                    .Children()
                    .Where(n => n.NodeId.NamespaceIndex == 2 && n.NodeId.ToString().StartsWith("ns=2;s=Device"))
                    .ToList();

                _devices.Clear();
                foreach (var node in deviceNodes)
                {
                    var name = node.DisplayName.Value;
                    try
                    {
                        var device = new OpcUaDevice(name, node.NodeId, _client);
                        device.Update();
                        _devices[name] = device;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OPC] Błąd podczas aktualizacji urządzenia '{name}': {ex.Message}");
                    }
                }

                return _devices.Values.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OPC] Błąd podczas pobierania urządzeń: {ex.Message}");
                return new List<OpcUaDevice>();
            }
        }
    }

    public class OpcUaDevice
    {
        public string Name { get; }
        public string? WorkorderId { get; private set; }
        public string ProductionStatus { get; private set; } = string.Empty;
        public int ProductionRate { get; private set; }
        public int GoodCount { get; private set; }
        public int BadCount { get; private set; }
        public float Temperature { get; private set; }

        private readonly OpcClient _client;
        private readonly OpcNodeId _baseNode;

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
                var nodeId = new OpcNodeId($"{Name}/{subPath}", 2);
                var value = _client.ReadNode(nodeId).Value;
                return (T)Convert.ChangeType(value!, typeof(T));
            }
            catch
            {
                return default!;
            }
        }

        public bool SetProductionRate(int rate)
        {
            if (rate < 0 || rate > 100 || rate % 10 != 0)
                return false;

            try
            {
                var nodeId = new OpcNodeId($"{Name}/ProductionRate", 2);
                _client.WriteNode(nodeId, rate);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SetEmergencyStop(bool status)
        {
            try
            {
                var methodName = status ? "EmergencyStop" : "ResetErrorStatus";
                var methodId = new OpcNodeId($"{Name}/{methodName}", 2);
                // Wywołaj metodę na serwerze OPC UA
                _client.CallMethod(_baseNode, methodId);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OPC] Błąd wywołania metody '{(status ? "EmergencyStop" : "ResetErrorStatus")}' dla urządzenia '{Name}': {ex.Message}");
                return false;
            }
        }
    }
}