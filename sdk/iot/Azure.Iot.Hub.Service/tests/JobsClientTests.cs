// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Threading.Tasks;
using Azure.Iot.Hub.Service.Models;
using FluentAssertions;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using NUnit.Framework;

namespace Azure.Iot.Hub.Service.Tests
{
    public class JobsClientTests : E2eTestBase
    {
        public JobsClientTests(bool isAsync)
            : base(isAsync)
        {
        }

        [Test]
        public async Task Jobs_Lifecycle()
        {
            // TODO: This is just a verification that tests run and it requires the tester to complete this test however they see fit.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=barustumrg1sa;AccountKey=xp1+xABVhJGvU4IpJldfzizl5184Hn+letTlmK0SzLlqbWo67ZQtzwyVzKPip7vEbooZAFnol8XAjLL+xElWDA==;EndpointSuffix=core.windows.net");
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer CloudBlobContainer = cloudBlobClient.GetContainerReference("jobs");

            Uri containerUri = CloudBlobContainer.Uri;
            var constraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read
                    | SharedAccessBlobPermissions.Write
                    | SharedAccessBlobPermissions.Create
                    | SharedAccessBlobPermissions.List
                    | SharedAccessBlobPermissions.Add
                    | SharedAccessBlobPermissions.Delete,
                SharedAccessStartTime = DateTimeOffset.UtcNow,
            };

            string sasContainerToken = CloudBlobContainer.GetSharedAccessSignature(constraints);
            Uri sasUri = new Uri($"{containerUri}{sasContainerToken}");

            IoTHubServiceClient client = GetClient();
            try
            {
                Response<JobProperties> response1 = await client.Jobs.CreateExportDevicesJobAsync(sasUri, false).ConfigureAwait(false);
                response1.GetRawResponse().Status.Should().Be(200);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
            }
        }
    }
}
