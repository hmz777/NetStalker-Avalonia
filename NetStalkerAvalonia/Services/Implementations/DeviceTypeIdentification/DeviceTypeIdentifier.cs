﻿using NetStalkerAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using NetStalkerAvalonia.Configuration;
using NetStalkerAvalonia.Helpers;
using Serilog;

namespace NetStalkerAvalonia.Services.Implementations.DeviceTypeIdentification
{
    public class DeviceTypeIdentifier : IDeviceTypeIdentifier
    {
        #region Members

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isStarted;
        private string? macLookupServiceUri;

        private readonly ILogger? _logger;
        private readonly HttpClient? _client;

        // The queue is to prevent flooding the mac lookup service with requests
        private Queue<Device> _identificationQueue;

        #endregion

        #region Constructor

        public DeviceTypeIdentifier(ILogger logger = null!,
            HttpClient client = null!)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = Tools.ResolveIfNull(logger);

            try
            {
                _client = Tools.ResolveIfNull(client);
            }
            catch (Exception e)
            {
                _logger.Warning("Service resolve error: {Message}",
                    e.Message);
            }

            macLookupServiceUri = ConfigurationManager
                .AppSettings[nameof(ConfigKeys.MacLookupServiceUri)];
            _identificationQueue = new Queue<Device>();
        }

        #endregion

        #region Internal

        private async Task StartIdentifierAsync()
        {
            while (_cancellationTokenSource.IsCancellationRequested == false)
            {
                try
                {
                    if (_identificationQueue.TryDequeue(out var deviceToIdentify))
                    {
                        var serviceUri = macLookupServiceUri!
                            .Replace("{Mac}", deviceToIdentify.Mac!
                                .ToString()
                                .Substring(0, 5));

                        var data = await _client!
                            .GetFromJsonAsync<DeviceIdentificationResponse>(serviceUri);

                        deviceToIdentify.SetVendor(data?.OrganizationName ?? "NAN");
                    }

                    await Task.Delay(3000);
                }
                catch (Exception e)
                {
                    _logger!.Error("Error in service of type: {Type}. Message: {Message}",
                        typeof(IDeviceTypeIdentifier),
                        e.Message);
                }
            }
        }

        #endregion

        #region API

        public async Task IdentifyDeviceAsync(Device device)
        {
            if (_isStarted == false)
            {
                await StartIdentifierAsync();

                _isStarted = true;
            }

            if (_identificationQueue.Contains(device) == false)
                _identificationQueue.Enqueue(device);
        }

        public void Dispose()
        {
            _isStarted = false;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        #endregion
    }
}