// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;

namespace Azure.Iot.Hub.Service.Authentication
{
    /// <summary>
    /// Implementation for parsing the supplied IoT Hub connection string.
    /// </summary>
    internal class IotHubConnectionString
    {
        private const string HostNameIdentifier = "HostName";
        private const string SharedAccessKeyIdentifier = "SharedAccessKey";
        private const string SharedAccessKeyNameIdentifier = "SharedAccessKeyName";

        internal IotHubConnectionString(string connectionString)
        {
            var iotHubConnectionString = ConnectionString.Parse(connectionString);

            SharedAccessKey = new AzureKeyCredential(iotHubConnectionString.GetRequired(SharedAccessKeyIdentifier));
            SharedAccessPolicy = iotHubConnectionString.GetRequired(SharedAccessKeyNameIdentifier);

            HostName = iotHubConnectionString.GetRequired(HostNameIdentifier);
        }

        internal string HostName { get; }

        internal string SharedAccessPolicy { get; }

        internal AzureKeyCredential SharedAccessKey { get; }
    }
}
