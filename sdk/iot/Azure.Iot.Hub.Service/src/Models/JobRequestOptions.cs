// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Iot.Hub.Service.Models
{
    /// <summary>
    /// Optional properties for the import and export jobs client library.
    /// </summary>
    public class JobRequestOptions
    {
        /// <summary>
        /// The name of the blob that will be created in the provided blob container. If not provided by the user, it will default to "devices.txt".
        /// </summary>
        /// <remarks>
        /// In the case of export, the blob will contain the export devices registry information for the IoT Hub. In the case of import, the blob will contain the status of importing devices.
        /// </remarks>
        public string BloblName { get; set; }

        /// <summary>
        /// Specifies authentication type being used for connecting to storage account. If not provided by the user, it will default to KeyBased type.
        /// </summary>
        public JobPropertiesStorageAuthenticationType AuthenticationType { get; set; }
    }
}
