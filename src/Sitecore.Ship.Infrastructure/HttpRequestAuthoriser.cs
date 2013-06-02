﻿using System.Web;
using Sitecore.Ship.Core;
using Sitecore.Ship.Infrastructure.Configuration;

namespace Sitecore.Ship.Infrastructure
{
    public class HttpRequestAuthoriser : IAuthoriser
    {
        private readonly HttpContextBase _context;
        private readonly IConfigurationProvider<PackageInstallationSettings> _configurationProvider;

        public HttpRequestAuthoriser(HttpContextBase context, IConfigurationProvider<PackageInstallationSettings> configurationProvider)
        {
            _context = context;
            _configurationProvider = configurationProvider;
        }

        public bool IsAllowed()
        {
            if (! _configurationProvider.Settings.IsEnabled) return false;

            if ((!_context.Request.IsLocal) && (!_configurationProvider.Settings.AllowRemoteAccess)) return false;

            if ((_context.Request.HttpMethod == "POST") && (!_configurationProvider.Settings.AllowPackageStreaming)) return false;

            return true;
        }
    }
}