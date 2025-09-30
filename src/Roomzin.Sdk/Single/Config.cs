using System;
using System.Collections.Generic;
using System.Linq;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Single
{
    public class Config
    {
        public string Host { get; set; } = string.Empty;
        public int TcpPort { get; set; }
        public string AuthToken { get; set; } = string.Empty;
        public TimeSpan Timeout { get; set; }
        public TimeSpan KeepAlive { get; set; }
    }

    public class ConfigBuilder
    {
        private Config _config;

        public ConfigBuilder()
        {
            _config = new Config
            {
                Timeout = TimeSpan.FromSeconds(2),
                KeepAlive = TimeSpan.FromSeconds(30)
            };
        }

        public ConfigBuilder WithHost(string host)
        {
            _config.Host = (host ?? string.Empty).Trim();
            return this;
        }

        public ConfigBuilder WithTcpPort(int port)
        {
            _config.TcpPort = port;
            return this;
        }

        public ConfigBuilder WithToken(string token)
        {
            _config.AuthToken = token ?? string.Empty;
            return this;
        }

        public ConfigBuilder WithTimeout(TimeSpan timeout)
        {
            _config.Timeout = timeout;
            return this;
        }

        public ConfigBuilder WithKeepAlive(TimeSpan keepAlive)
        {
            _config.KeepAlive = keepAlive;
            return this;
        }

        public Config Build()
        {
            Validate();
            return _config;
        }

        private void Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_config.Host))
                errors.Add("Server address is required");

            if (_config.TcpPort == 0)
                errors.Add("TCP port is required");

            if (string.IsNullOrWhiteSpace(_config.AuthToken))
                errors.Add("Authentication requires a token");

            if (errors.Count > 0)
                throw RoomzinException.From(string.Join("; ", errors));
        }
    }
}