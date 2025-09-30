using System;
using System.Collections.Generic;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Cluster
{
    public class Config
    {
        public string SeedNodeIds { get; set; } = string.Empty; // "node1,node2,node3"
        public int ApiPort { get; set; } // HTTP port for /peers /leader /node-info
        public int TcpPort { get; set; } // TCP port for framed protocol
        public string AuthToken { get; set; } = string.Empty;
        public TimeSpan Timeout { get; set; }
        public TimeSpan HttpTimeout { get; set; }
        public TimeSpan KeepAlive { get; set; }
        public int MaxActiveConns { get; set; } // hard cap on open TCP connections
        public TimeSpan NodeProbeInterval { get; set; } // how often to health-check
        public string DiscoveryAddr { get; set; } = string.Empty; // if set → HTTP discovery mode
        public List<NodeAddr> StaticDiscovery { get; set; } = new(); // used only when DiscoveryAddr is empty
    }
}