// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Iot.Hub.Service.Models;
using FluentAssertions;
using Microsoft.Azure.Devices.Client;
using NUnit.Framework;

namespace Azure.Iot.Hub.Service.Tests
{
    /// <summary>
    /// Test all APIs of a DeviceClient.
    /// </summary>
    /// <remarks>
    /// All API calls are wrapped in a try catch block so we can clean up resources regardless of the test outcome.
    /// </remarks>
    public class ModulesClientTests : E2eTestBase
    {
        private const int BULK_DEVICE_COUNT = 10;
        private readonly TimeSpan _queryMaxWaitTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _queryRetryInterval = TimeSpan.FromSeconds(2);

        public ModulesClientTests(bool isAsync)
            : base(isAsync)
        {
        }

        /// <summary>
        /// Test basic lifecycle of a Device Identity.
        /// This test includes CRUD operations only.
        /// </summary>
        [Test]
        public async Task ModulesClient_IdentityLifecycle()
        {
            string testDeviceId = $"IdentityLifecycleDevice{GetRandom()}";
            string testModuleId = $"IdentityLifecycleModule{GetRandom()}";

            DeviceIdentity device = null;
            ModuleIdentity module = null;
            IoTHubServiceClient client = GetClient();

            try
            {
                // Create a device to house the module
                device = (await client.Devices.CreateOrUpdateIdentityAsync(
                    new DeviceIdentity
                    {
                        DeviceId = testDeviceId
                    })).Value;

                // Create a module on the device
                Response<ModuleIdentity> createResponse = await client.Modules.CreateOrUpdateIdentityAsync(
                    new ModuleIdentity
                    {
                        DeviceId = testDeviceId,
                        ModuleId = testModuleId
                    }).ConfigureAwait(false);

                module = createResponse.Value;

                module.DeviceId.Should().Be(testDeviceId);
                module.ModuleId.Should().Be(testModuleId);

                // Get device
                // Get the device and compare ETag values (should remain unchanged);
                Response<ModuleIdentity> getResponse = await client.Modules.GetIdentityAsync(testDeviceId, testModuleId).ConfigureAwait(false);

                getResponse.Value.Etag.Should().BeEquivalentTo(module.Etag, "ETag value should not have changed.");

                module = getResponse.Value;

                // Update a module
                string managedByValue = "SomeChangedValue";
                module.ManagedBy = managedByValue;

                // TODO: (azabbasi) We should leave the IfMatchPrecondition to be the default value once we know more about the fix.
                Response<ModuleIdentity> updateResponse = await client.Modules.CreateOrUpdateIdentityAsync(module, IfMatchPrecondition.UnconditionalIfMatch).ConfigureAwait(false);

                updateResponse.Value.ManagedBy.Should().Be(managedByValue, "Module should have changed its managedBy value");

                // Delete the device
                // Deleting the device happens in the finally block as cleanup.
            }
            finally
            {
                await Cleanup(client, device);
            }
        }

        /// <summary>
        /// Test the logic for ETag if-match header
        /// </summary>
        [Test]
        public async Task ModulesClient_UpdateDevice_EtagDoesNotMatch()
        {
            string testDeviceId = $"UpdateWithETag{GetRandom()}";
            string testModuleId = $"UpdateWithETag{GetRandom()}";

            DeviceIdentity device = null;
            ModuleIdentity module = null;
            IoTHubServiceClient client = GetClient();

            try
            {
                // Create a device
                device = (await client.Devices.CreateOrUpdateIdentityAsync(
                    new Models.DeviceIdentity
                    {
                        DeviceId = testDeviceId
                    }).ConfigureAwait(false)).Value;

                // Create a module on that device
                module = (await client.Modules.CreateOrUpdateIdentityAsync(
                    new Models.ModuleIdentity
                    {
                        DeviceId = testDeviceId,
                        ModuleId = testModuleId
                    }).ConfigureAwait(false)).Value;

                // Update the module to get a new ETag value.
                string managedByValue = "SomeChangedValue";
                module.ManagedBy = managedByValue;

                ModuleIdentity updatedModule = (await client.Modules.CreateOrUpdateIdentityAsync(module).ConfigureAwait(false)).Value;

                Assert.AreNotEqual(updatedModule.Etag, module.Etag, "ETag should have been updated.");

                // Perform another update using the old device object to verify precondition fails.
                string anotherManagedByValue = "SomeOtherChangedValue";
                module.ManagedBy = anotherManagedByValue;
                try
                {
                    await client.Modules.CreateOrUpdateIdentityAsync(module, IfMatchPrecondition.IfMatch).ConfigureAwait(false);
                    Assert.Fail($"Update call with outdated ETag should fail with 412 (PreconditionFailed)");
                }
                // We will catch the exception and verify status is 412 (PreconditionfFailed)
                catch (RequestFailedException ex)
                {
                    Assert.AreEqual(412, ex.Status, $"Expected the update to fail with http status code 412 (PreconditionFailed)");
                }

                // Perform the same update and ignore the ETag value by providing UnconditionalIfMatch precondition
                ModuleIdentity forcefullyUpdatedModule = (await client.Modules.CreateOrUpdateIdentityAsync(module, IfMatchPrecondition.UnconditionalIfMatch).ConfigureAwait(false)).Value;
                forcefullyUpdatedModule.ManagedBy.Should().Be(anotherManagedByValue);
            }
            finally
            {
                await Cleanup(client, device);
            }
        }

        /// <summary>
        /// Test basic operations of a Device Twin.
        /// </summary>
        [Test]
        public async Task ModulesClient_DeviceTwinLifecycle()
        {
            string testDeviceId = $"TwinLifecycleDevice{GetRandom()}";
            string testModuleId = $"TwinLifecycleModule{GetRandom()}";

            DeviceIdentity device = null;
            ModuleIdentity module = null;

            IoTHubServiceClient client = GetClient();

            try
            {
                // Create a device
                device = (await client.Devices.CreateOrUpdateIdentityAsync(
                    new Models.DeviceIdentity
                    {
                        DeviceId = testDeviceId
                    }).ConfigureAwait(false)).Value;

                // Create a module on that device. Note that this implicitly creates the module twin
                module = (await client.Modules.CreateOrUpdateIdentityAsync(
                    new Models.ModuleIdentity
                    {
                        DeviceId = testDeviceId,
                        ModuleId = testModuleId
                    }).ConfigureAwait(false)).Value;

                // Get the module twin
                TwinData moduleTwin = (await client.Modules.GetTwinAsync(testDeviceId, testModuleId).ConfigureAwait(false)).Value;

                moduleTwin.ModuleId.Should().BeEquivalentTo(testModuleId, "ModuleId on the Twin should match that of the module identity.");

                // Update device twin
                string propName = "username";
                string propValue = "userA";
                moduleTwin.Properties.Desired.Add(new KeyValuePair<string, object>(propName, propValue));

                // TODO: (azabbasi) We should leave the IfMatchPrecondition to be the default value once we know more about the fix.
                Response<TwinData> updateResponse = await client.Modules.UpdateTwinAsync(moduleTwin, IfMatchPrecondition.UnconditionalIfMatch).ConfigureAwait(false);

                updateResponse.Value.Properties.Desired.Where(p => p.Key == propName).First().Value.Should().Be(propValue, "Desired property value is incorrect.");

                // Delete the module
                // Deleting the module happens in the finally block as cleanup.
            }
            finally
            {
                await Cleanup(client, device);
            }
        }

        /// <summary>
        /// Test that ETag and If-Match headers work as expected
        /// </summary>
        [Test]
        public async Task ModulesClient_UpdateModuleTwin_EtagDoesNotMatch()
        {
            string testDeviceId = $"TwinLifecycleDevice{GetRandom()}";
            string testModuleId = $"TwinLifecycleModule{GetRandom()}";

            DeviceIdentity device = null;
            ModuleIdentity module = null;

            IoTHubServiceClient client = GetClient();

            try
            {
                // Create a device
                device = (await client.Devices.CreateOrUpdateIdentityAsync(
                    new Models.DeviceIdentity
                    {
                        DeviceId = testDeviceId
                    }).ConfigureAwait(false)).Value;

                // Create a module on that device. Note that this implicitly creates the module twin
                module = (await client.Modules.CreateOrUpdateIdentityAsync(
                    new Models.ModuleIdentity
                    {
                        DeviceId = testDeviceId,
                        ModuleId = testModuleId
                    }).ConfigureAwait(false)).Value;

                // Get the module twin
                TwinData moduleTwin = (await client.Modules.GetTwinAsync(testDeviceId, testModuleId).ConfigureAwait(false)).Value;

                moduleTwin.ModuleId.Should().BeEquivalentTo(testModuleId, "ModuleId on the Twin should match that of the module identity.");

                // Update device twin
                string propName = "username";
                string propValue = "userA";
                moduleTwin.Properties.Desired.Add(new KeyValuePair<string, object>(propName, propValue));

                // TODO: (azabbasi) We should leave the IfMatchPrecondition to be the default value once we know more about the fix.
                Response<TwinData> updateResponse = await client.Modules.UpdateTwinAsync(moduleTwin, IfMatchPrecondition.UnconditionalIfMatch).ConfigureAwait(false);

                updateResponse.Value.Properties.Desired.Where(p => p.Key == propName).First().Value.Should().Be(propValue, "Desired property value is incorrect.");

                // Perform another update using the old device object to verify precondition fails.
                try
                {
                    // Try to update the twin with the previously up-to-date twin
                    await client.Modules.UpdateTwinAsync(moduleTwin, IfMatchPrecondition.IfMatch).ConfigureAwait(false);
                    Assert.Fail($"Update call with outdated ETag should fail with 412 (PreconditionFailed)");
                }
                // We will catch the exception and verify status is 412 (PreconditionfFailed)
                catch (RequestFailedException ex)
                {
                    Assert.AreEqual(412, ex.Status, $"Expected the update to fail with http status code 412 (PreconditionFailed)");
                }

                // Delete the module
                // Deleting the module happens in the finally block as cleanup.
            }
            finally
            {
                await Cleanup(client, device);
            }
        }

        [Test]
        public async Task ModulesClient_GetModulesOnDevice()
        {
            int moduleCount = 5;
            string testDeviceId = $"IdentityLifecycleDevice{GetRandom()}";
            string[] testModuleIds = new string[moduleCount];
            for (int i = 0; i < moduleCount; i++)
            {
                testModuleIds[i] = $"IdentityLifecycleModule{i}-{GetRandom()}";
            }

            DeviceIdentity device = null;
            IoTHubServiceClient client = GetClient();

            try
            {
                // Create a device to house the modules
                device = (await client.Devices.CreateOrUpdateIdentityAsync(
                    new DeviceIdentity
                    {
                        DeviceId = testDeviceId
                    })).Value;

                // Create the modules on the device
                for (int i = 0; i < moduleCount; i++)
                {
                    Response<ModuleIdentity> createResponse = await client.Modules.CreateOrUpdateIdentityAsync(
                    new ModuleIdentity
                    {
                        DeviceId = testDeviceId,
                        ModuleId = testModuleIds[i]
                    }).ConfigureAwait(false);
                }

                // List the modules on the test device
                IReadOnlyList<ModuleIdentity> modulesOnDevice = (await client.Modules.GetIdentitiesAsync(testDeviceId).ConfigureAwait(false)).Value;

                IEnumerable<string> moduleIdsOnDevice = modulesOnDevice
                    .ToList()
                    .Select(module => module.ModuleId);

                Assert.AreEqual(moduleCount, modulesOnDevice.Count);
                for (int i = 0; i < moduleCount; i++)
                {
                    Assert.IsTrue(moduleIdsOnDevice.Contains(testModuleIds[i]));
                }
            }
            finally
            {
                await Cleanup(client, device);
            }
        }

        [Test]
        public async Task ModulesClient_InvokeMethodOnModule()
        {
            if (!this.IsAsync)
            {
                // TODO: Tim: The module client doesn't appear to open a connection to iothub or start
                // listening for method invocations when this test is run in Sync mode. Not sure why though.
                // calls to track 1 library don't throw, but seem to silently fail
                return;
            }

            string testDeviceId = $"InvokeMethodDevice{GetRandom()}";
            string testModuleId = $"InvokeMethodModule{GetRandom()}";

            DeviceIdentity device = null;
            ModuleIdentity module = null;
            ModuleClient moduleClient = null;
            IoTHubServiceClient serviceClient = GetClient();

            try
            {
                // Create a device to house the modules
                device = (await serviceClient.Devices.CreateOrUpdateIdentityAsync(
                    new DeviceIdentity
                    {
                        DeviceId = testDeviceId
                    })).Value;

                // Create the module on the device
                module = (await serviceClient.Modules.CreateOrUpdateIdentityAsync(
                    new ModuleIdentity
                    {
                        DeviceId = testDeviceId,
                        ModuleId = testModuleId
                    }).ConfigureAwait(false)).Value;

                // Method expectations
                string expectedMethodName = "someMethodToInvoke";
                int expectedStatus = 222;
                object expectedRequestPayload = null;

                // Create module client instance to receive the method invocation
                string moduleClientConnectionString = $"HostName={GetHostName()};DeviceId={testDeviceId};ModuleId={testModuleId};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey}";
                moduleClient = ModuleClient.CreateFromConnectionString(moduleClientConnectionString);

                // These two methods are part of our track 1 device client. When the test fixture runs when isAsync = true,
                // these methods work. When isAsync = false, these methods silently don't work.
                await moduleClient.OpenAsync();
                await moduleClient.SetMethodHandlerAsync(
                    expectedMethodName,
                    (methodRequest, userContext) =>
                    {
                        return Task.FromResult(new MethodResponse(expectedStatus));
                    },
                    null).ConfigureAwait(false);

                // Invoke the method on the module
                CloudToDeviceMethodRequest methodRequest = new CloudToDeviceMethodRequest()
                {
                    MethodName = expectedMethodName,
                    Payload = expectedRequestPayload,
                    ConnectTimeoutInSeconds = 5,
                    ResponseTimeoutInSeconds = 5
                };

                var methodResponse = (await serviceClient.Modules.InvokeMethodAsync(testDeviceId, testModuleId, methodRequest).ConfigureAwait(false)).Value;

                Assert.AreEqual(expectedStatus, methodResponse.Status);
            }
            finally
            {
                if (moduleClient != null)
                {
                    await moduleClient.CloseAsync().ConfigureAwait(false);
                }

                await Cleanup(serviceClient, device);
            }
        }

        private async Task Cleanup(IoTHubServiceClient client, DeviceIdentity device)
        {
            // cleanup
            try
            {
                if (device != null)
                {
                    await client.Devices.DeleteIdentityAsync(device, IfMatchPrecondition.UnconditionalIfMatch).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"Test clean up failed: {ex.Message}");
            }
        }
    }
}
