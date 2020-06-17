// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Iot.Hub.Service.Models;

namespace Azure.Iot.Hub.Service
{
    /// <summary>
    /// Jobs client to support Import/Export and Scheduled Jobs.
    /// </summary>
    public class JobsClient
    {
        private readonly JobRestClient _jobRestClient;

        protected JobsClient()
        {
            // This constructor only exists for mocking purposes.
        }

        internal JobsClient(JobRestClient jobRestClient)
        {
            Argument.AssertNotNull(jobRestClient, nameof(jobRestClient));

            _jobRestClient = jobRestClient;
        }

        /// <summary>
        /// Creates a job to export device registrations to the container.
        /// </summary>
        /// <param name="outputBlobContainerUri">URI containing SAS token to a blob container. This is used to output the results of the export job.</param>
        /// <param name="excludeKeys">If false, authorization keys are included in export output.</param>
        /// <param name="options">The optional settings for this request.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>JobProperties of the newly created job.</returns>
        public virtual Response<JobProperties> CreateExportDevicesJob(
            Uri outputBlobContainerUri,
            bool excludeKeys,
            JobRequestOptions options = default,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(outputBlobContainerUri, nameof(outputBlobContainerUri));

            var jobProperties = new JobProperties
            {
                OutputBlobContainerUri = outputBlobContainerUri.ToString(),
                ExcludeKeysInExport = excludeKeys,
                StorageAuthenticationType = options?.AuthenticationType,
                OutputBlobName = options?.BloblName
            };

            return _jobRestClient.CreateImportExportJob(jobProperties, cancellationToken);
        }

        /// <summary>
        /// Creates a job to export device registrations to the container.
        /// </summary>
        /// <param name="outputBlobContainerUri">URI containing SAS token to a blob container. This is used to output the results of the export job.</param>
        /// <param name="excludeKeys">If false, authorization keys are included in export output.</param>
        /// <param name="options">The optional settings for this request.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>JobProperties of the newly created job.</returns>
        public virtual Task<Response<JobProperties>> CreateExportDevicesJobAsync(Uri outputBlobContainerUri, bool excludeKeys, JobRequestOptions options = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(outputBlobContainerUri, nameof(outputBlobContainerUri));

            var jobProperties = new JobProperties
            {
                OutputBlobContainerUri = outputBlobContainerUri.ToString(),
                ExcludeKeysInExport = excludeKeys,
                StorageAuthenticationType = options?.AuthenticationType,
                OutputBlobName = options?.BloblName
            };

            return _jobRestClient.CreateImportExportJobAsync(jobProperties, cancellationToken);
        }
    }
}
