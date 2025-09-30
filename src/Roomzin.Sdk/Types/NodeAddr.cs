using System.Text.Json.Serialization;

namespace Roomzin.Sdk.Types
{
    /// <summary>
    /// Represents a node address in the cluster.
    /// </summary>
    public class NodeAddr
    {
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }

        [JsonPropertyName("addr")]
        public string Addr { get; set; }

        [JsonPropertyName("tcp_port")]
        public int TcpPort { get; set; }

        [JsonPropertyName("api_port")]
        public int ApiPort { get; set; }

        public NodeAddr(string nodeId, string addr, int tcpPort, int apiPort)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            Addr = addr ?? throw new ArgumentNullException(nameof(addr));
            TcpPort = tcpPort;
            ApiPort = apiPort;
        }

        public override string ToString()
        {
            return $"NodeAddr {{ NodeId = {NodeId}, Addr = {Addr}, TcpPort = {TcpPort}, ApiPort = {ApiPort} }}";
        }
    }
}