using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Cluster
{
    public class NodeInfo
    {
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("zone_id")]
        public string ZoneId { get; set; } = string.Empty;

        [JsonPropertyName("shard_id")]
        public string ShardId { get; set; } = string.Empty;

        [JsonPropertyName("leader_id")]
        public string LeaderId { get; set; } = string.Empty;
    }

    public static class ClusterHelper
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task<(NodeAddr Leader, List<NodeAddr> Followers)> GetClusterInfoAsync(Config cfg, DiscoveryMap dmap, CancellationToken cancellationToken = default)
        {
            var nodeIds = ParseNodeIds(cfg.SeedNodeIds);
            if (nodeIds.Length == 0)
            {
                throw RoomzinException.From("no seed node IDs provided");
            }

            var existing = new ConcurrentDictionary<string, bool>(
                nodeIds.ToDictionary(id => id, id => true)
            );

            var discovered = new ConcurrentDictionary<string, bool>();
            var nodes = new ConcurrentDictionary<string, NodeInfoResult>();

            // First phase: seed nodes
            var firstPhaseTasks = new List<Task>();
            foreach (var nodeId in nodeIds)
            {
                firstPhaseTasks.Add(ProcessNodeAsync(nodeId, existing, discovered, nodes, cfg, dmap, cancellationToken));
            }

            await Task.WhenAll(firstPhaseTasks);

            // Second phase: discovered nodes
            var discoveredNodeIds = discovered.Keys.ToList();
            if (discoveredNodeIds.Count > 0)
            {
                var secondPhaseTasks = new List<Task>();
                foreach (var nodeId in discoveredNodeIds)
                {
                    if (!nodes.Values.Any(n => n.NodeId == nodeId))
                    {
                        secondPhaseTasks.Add(ProcessDiscoveredNodeAsync(nodeId, nodes, cfg, dmap, cancellationToken));
                    }
                }

                await Task.WhenAll(secondPhaseTasks);
            }

            // Third phase: determine leader using voting system
            return DetermineLeader(nodes);
        }

        private static async Task ProcessNodeAsync(string nodeId, ConcurrentDictionary<string, bool> existing,
            ConcurrentDictionary<string, bool> discovered, ConcurrentDictionary<string, NodeInfoResult> nodes,
            Config cfg, DiscoveryMap dmap, CancellationToken cancellationToken)
        {
            var resolved = dmap.Resolve(nodeId);
            if (resolved == null) return;

            string host = resolved.Host;
            int apiPort = resolved.ApiPort;
            int tcpPort = resolved.TcpPort;

            try
            {
                var health = await HealthCheckAsync(host, apiPort, cfg, cancellationToken);
                if (string.IsNullOrEmpty(health) || health == "unavailable")
                {
                    return;
                }

                var nodeInfo = await GetNodeInfoInternalAsync(host, apiPort, cfg, cancellationToken);
                if (nodeInfo != null)
                {
                    nodes[host] = new NodeInfoResult
                    {
                        NodeId = nodeId,
                        Host = host,
                        TcpPort = tcpPort,
                        ApiPort = apiPort,
                        Health = health,
                        LeaderId = nodeInfo.LeaderId ?? string.Empty
                    };

                    // Discover peers
                    var peers = await GetPeersAsync(host, apiPort, cfg, cancellationToken);
                    foreach (var peerId in peers)
                    {
                        if (!existing.ContainsKey(peerId) && !discovered.ContainsKey(peerId))
                        {
                            discovered.TryAdd(peerId, true);
                        }
                    }
                }
            }
            catch
            {
                // Ignore failed nodes
            }
        }

        private static async Task ProcessDiscoveredNodeAsync(string nodeId, ConcurrentDictionary<string, NodeInfoResult> nodes,
            Config cfg, DiscoveryMap dmap, CancellationToken cancellationToken)
        {
            var resolved = dmap.Resolve(nodeId);
            if (resolved == null) return;

            string host = resolved.Host;
            int apiPort = resolved.ApiPort;
            int tcpPort = resolved.TcpPort;

            try
            {
                var health = await HealthCheckAsync(host, apiPort, cfg, cancellationToken);
                if (string.IsNullOrEmpty(health) || health == "unavailable")
                    return;

                var nodeInfo = await GetNodeInfoInternalAsync(host, apiPort, cfg, cancellationToken);
                if (nodeInfo != null)
                {
                    nodes[host] = new NodeInfoResult
                    {
                        NodeId = nodeId,
                        Host = host,
                        TcpPort = tcpPort,
                        ApiPort = apiPort,
                        Health = health,
                        LeaderId = nodeInfo.LeaderId ?? string.Empty
                    };
                }
            }
            catch
            {
                // Ignore failed nodes
            }
        }

        private static (NodeAddr Leader, List<NodeAddr> Followers) DetermineLeader(ConcurrentDictionary<string, NodeInfoResult> nodes)
        {
            var votes = new Dictionary<string, int>();
            foreach (var node in nodes.Values)
            {
                if (!string.IsNullOrEmpty(node.LeaderId))
                {
                    votes[node.LeaderId] = votes.GetValueOrDefault(node.LeaderId, 0) + 1;
                }
            }

            if (votes.Count == 0)
            {
                throw RoomzinException.From("no leader available");
            }

            var leaderId = string.Empty;
            var maxVotes = 0;
            foreach (var (id, count) in votes)
            {
                if (count > maxVotes)
                {
                    maxVotes = count;
                    leaderId = id;
                }
            }

            if (string.IsNullOrEmpty(leaderId))
            {
                throw RoomzinException.From("no leader available");
            }

            NodeAddr? leader = null;
            var followers = new List<NodeAddr>();

            foreach (var node in nodes.Values)
            {
                if (node.LeaderId == leaderId)
                {
                    var addr = new NodeAddr(
                        node.NodeId,
                        node.Host,
                        node.TcpPort,
                        node.ApiPort
                    );

                    if (string.Equals(node.Health, "active_leader", StringComparison.OrdinalIgnoreCase))
                    {
                        leader = addr;
                    }
                    else if (string.Equals(node.Health, "active_follower", StringComparison.OrdinalIgnoreCase))
                    {
                        followers.Add(addr);
                    }
                }
            }

            if (leader == null)
            {
                throw RoomzinException.From("no leader available");
            }

            return (leader, followers);
        }

        private static string[] ParseNodeIds(string seedNodeIds)
        {
            return seedNodeIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToArray();
        }

        private static async Task<List<string>> GetPeersAsync(string host, int apiPort, Config cfg, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"http://{host}:{apiPort}/peers";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (!string.IsNullOrEmpty(cfg.AuthToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.AuthToken);
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                using var response = await HttpClient.SendAsync(request, linkedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<string>();
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                }) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static async Task<string> HealthCheckAsync(string host, int apiPort, Config cfg, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"http://{host}:{apiPort}/healthz";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (!string.IsNullOrEmpty(cfg.AuthToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.AuthToken);
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                using var response = await HttpClient.SendAsync(request, linkedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return "unavailable";
                }

                var body = await response.Content.ReadAsStringAsync();
                return body.Trim();
            }
            catch
            {
                return "unavailable";
            }
        }

        private static async Task<NodeInfo?> GetNodeInfoInternalAsync(string host, int apiPort, Config cfg, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"http://{host}:{apiPort}/node-info";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (!string.IsNullOrEmpty(cfg.AuthToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.AuthToken);
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                using var response = await HttpClient.SendAsync(request, linkedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<NodeInfo>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private class NodeInfoResult
        {
            public string NodeId { get; set; } = string.Empty;
            public string Host { get; set; } = string.Empty;
            public int TcpPort { get; set; }
            public int ApiPort { get; set; }
            public string Health { get; set; } = string.Empty;
            public string LeaderId { get; set; } = string.Empty;
        }
    }
}