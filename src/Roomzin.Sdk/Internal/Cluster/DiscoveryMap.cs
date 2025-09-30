using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Cluster
{
    /// <summary>
    /// Thread-safe discovery map for resolving node IDs to addresses.
    /// Used by the cluster handler to resolve node IDs to host/port combinations.
    /// </summary>
    public class DiscoveryMap
    {
        private readonly ConcurrentDictionary<string, ResolvedAddr> _data = new();

        /// <summary>
        /// Resolved address information for a node.
        /// </summary>
        public class ResolvedAddr
        {
            public string Host { get; }
            public int TcpPort { get; }
            public int ApiPort { get; }

            public ResolvedAddr(string host, int tcpPort, int apiPort)
            {
                Host = host ?? throw new ArgumentNullException(nameof(host));
                TcpPort = tcpPort;
                ApiPort = apiPort;
            }
        }

        /// <summary>
        /// Resolves a node ID to its address information.
        /// </summary>
        /// <param name="nodeId">The node ID to resolve</param>
        /// <returns>ResolvedAddr if found, null otherwise</returns>
        public ResolvedAddr? Resolve(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return null;

            _data.TryGetValue(nodeId, out var resolved);
            return resolved;
        }

        /// <summary>
        /// Updates the discovery map with new nodes (used by HTTP discovery mode).
        /// </summary>
        /// <param name="nodes">List of NodeAddr objects</param>
        /// <param name="defaultTcpPort">Default TCP port if not specified</param>
        /// <param name="defaultApiPort">Default API port if not specified</param>
        public void Update(IEnumerable<NodeAddr> nodes, int defaultTcpPort, int defaultApiPort)
        {
            if (nodes == null) return;

            var newData = new ConcurrentDictionary<string, ResolvedAddr>();
            foreach (var node in nodes)
            {
                int tcpPort = node.TcpPort > 0 ? node.TcpPort : defaultTcpPort;
                int apiPort = node.ApiPort > 0 ? node.ApiPort : defaultApiPort;
                newData[node.NodeId] = new ResolvedAddr(node.Addr, tcpPort, apiPort);
            }

            _data.Clear();
            foreach (var kvp in newData)
            {
                _data[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Sets static discovery data (used by static discovery mode).
        /// </summary>
        /// <param name="nodes">List of NodeAddr objects</param>
        /// <param name="defaultTcpPort">Default TCP port if not specified</param>
        /// <param name="defaultApiPort">Default API port if not specified</param>
        public void SetStatic(IEnumerable<NodeAddr> nodes, int defaultTcpPort, int defaultApiPort)
        {
            _data.Clear();
            if (nodes == null) return;

            foreach (var node in nodes)
            {
                int tcpPort = node.TcpPort > 0 ? node.TcpPort : defaultTcpPort;
                int apiPort = node.ApiPort > 0 ? node.ApiPort : defaultApiPort;
                _data[node.NodeId] = new ResolvedAddr(node.Addr, tcpPort, apiPort);
            }
        }

        /// <summary>
        /// Returns the number of entries in the discovery map.
        /// </summary>
        public int Count => _data.Count;

        /// <summary>
        /// Checks if the discovery map is empty.
        /// </summary>
        public bool IsEmpty => _data.IsEmpty;
    }
}