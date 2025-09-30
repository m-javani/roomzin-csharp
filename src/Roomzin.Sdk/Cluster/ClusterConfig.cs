using System;
using System.Collections.Generic;
using System.Linq;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Cluster
{
    public class ClusterConfig
    {
        public string SeedNodeIds { get; set; } = string.Empty; // "node1,node2,node3"
        public int ApiPort { get; set; } // HTTP port for /peers /leader /node-info
        public int TcpPort { get; set; } // TCP port for framed protocol
        public string AuthToken { get; set; } = string.Empty;
        public TimeSpan Timeout { get; set; }
        public TimeSpan HttpTimeout { get; set; }
        public TimeSpan KeepAlive { get; set; }
        public int MaxActiveConns { get; set; } // hard cap on open TCP connections
        public string DiscoveryAddr { get; set; } = string.Empty; // if set → HTTP discovery mode
        public List<NodeAddr> StaticDiscovery { get; set; } = new(); // used only when DiscoveryAddr is empty
    }

    public class ClusterConfigBuilder
    {
        private ClusterConfig _config;

        public ClusterConfigBuilder()
        {
            _config = new ClusterConfig
            {
                Timeout = TimeSpan.FromSeconds(2),
                HttpTimeout = TimeSpan.FromSeconds(2),
                KeepAlive = TimeSpan.FromSeconds(30),
                MaxActiveConns = 10,
                StaticDiscovery = new List<NodeAddr>()
            };
        }

        public ClusterConfigBuilder WithSeedNodeIds(string seed)
        {
            _config.SeedNodeIds = (seed ?? string.Empty).Trim();
            return this;
        }

        public ClusterConfigBuilder WithApiPort(int port)
        {
            _config.ApiPort = port;
            return this;
        }

        public ClusterConfigBuilder WithTcpPort(int port)
        {
            _config.TcpPort = port;
            return this;
        }

        public ClusterConfigBuilder WithToken(string token)
        {
            _config.AuthToken = token ?? string.Empty;
            return this;
        }

        public ClusterConfigBuilder WithTimeout(TimeSpan timeout)
        {
            _config.Timeout = timeout;
            return this;
        }

        public ClusterConfigBuilder WithHttpTimeout(TimeSpan httpTimeout)
        {
            _config.HttpTimeout = httpTimeout;
            return this;
        }

        public ClusterConfigBuilder WithKeepAlive(TimeSpan keepAlive)
        {
            _config.KeepAlive = keepAlive;
            return this;
        }

        public ClusterConfigBuilder WithMaxActiveConns(int maxConns)
        {
            _config.MaxActiveConns = maxConns > 0 ? maxConns : 10;
            return this;
        }

        public ClusterConfigBuilder WithDiscoveryAddr(string discoveryAddr)
        {
            _config.DiscoveryAddr = (discoveryAddr ?? string.Empty).Trim();
            return this;
        }

        public ClusterConfigBuilder WithStaticDiscovery(List<NodeAddr> staticDiscovery)
        {
            _config.StaticDiscovery = staticDiscovery ?? new List<NodeAddr>();
            return this;
        }

        public ClusterConfig Build()
        {
            Validate();
            return _config;
        }

        private void Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_config.SeedNodeIds))
                errors.Add("At least one seed node ID is required");

            if (_config.TcpPort == 0)
                errors.Add("TCP port is required");

            if (_config.ApiPort == 0)
                errors.Add("API port is required in clustered mode");

            if (string.IsNullOrWhiteSpace(_config.AuthToken))
                errors.Add("Authentication requires a token");

            if (errors.Count > 0)
                throw RoomzinException.From(string.Join("; ", errors));
        }
    }
}