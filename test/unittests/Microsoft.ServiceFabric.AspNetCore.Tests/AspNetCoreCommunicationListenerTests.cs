﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.AspNetCore.Tests
{
    using System;
    using System.Fabric.Description;
    using System.Threading;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
    using FluentAssertions;
    using Moq;

    public class AspNetCoreCommunicationListenerTests
    {
        protected string UrlFromListenerToBuildFunc { get; private set; }        
        protected const string DefaultExpectedProtocol = "http";
        protected const string DefaultPathPrefix = "PathPrefix";
        protected const int DefaultExpectedPort = 0;
        internal static EndpointProtocol ExpectedProtocolFromEndpoint = EndpointProtocol.Http;
        internal static string EndpointName = "MockEndpoint";

        /// <summary>
        /// Property used by test to determine if WebHost was started/stopped.
        /// </summary>
        protected bool IsStarted { get; set; }

        /// <summary>
        /// Listener used by the test.
        /// </summary>
        protected AspNetCoreCommunicationListener Listener { get;  set; }

        /// <summary>
        /// Used by tests to configure ServiceFabricIntegrationOption
        /// </summary>
        protected ServiceFabricIntegrationOptions IntegrationOptions { get; set; }        

        protected EndpointResourceDescription GetTestEndpoint()
        {
            return new EndpointResourceDescription()
            {
                Name = EndpointName,
                Protocol = ExpectedProtocolFromEndpoint
                //Port = ExpectedPortFromEndpoint
            };
        }

        public IWebHost BuildFunc(string url, AspNetCoreCommunicationListener listener)
        {
            this.UrlFromListenerToBuildFunc = url;

            // Create mock IServerAddressesFeature and set required things used by tests.
            var mockServerAddressFeature = new Mock<IServerAddressesFeature>();
            mockServerAddressFeature.Setup(y => y.Addresses).Returns(new string[] { url });
            var featureColelction = new FeatureCollection();
            featureColelction.Set(mockServerAddressFeature.Object);

            // Create mock IWebHost and set required things used by tests.
            var mockWebHost = new Mock<IWebHost>();
            mockWebHost.Setup(y => y.ServerFeatures).Returns(featureColelction);

            // setup call backs for Start , Dispose.
            mockWebHost.Setup(y => y.Start()).Callback(() => this.IsStarted = true);
            mockWebHost.Setup(y => y.Dispose()).Callback(() => this.IsStarted = false);

            // tell listener whether to generate UniqueServiceUrls
            if (this.IntegrationOptions.Equals(ServiceFabricIntegrationOptions.UseUniqueServiceUrl))
            {
                listener.ConfigureToUseUniqueServiceUrl();
            }

            return mockWebHost.Object;
        }


        /// <summary>
        /// Tests Url for ServiceFabricIntegrationOptions.UseUniqueServiceUrl
        /// 1. When endpoint name is provided (protocol and port comes from endpoint.) :
        ///   a. url given to Func to create IWebHost should be protocol://+:port. 
        ///   b. url returned from OpenAsync should be protocol://IPAddressOrFQDN:port/PartitionId/ReplicaId
        /// 
        /// </summary>        
        protected void UseUniqueServiceUrlOptionVerifier()
        {
            this.IntegrationOptions = ServiceFabricIntegrationOptions.UseUniqueServiceUrl;
            var expectedProtocol = ExpectedProtocolFromEndpoint.ToString().ToLower();
            var expectedPort = DefaultExpectedPort;

            Console.WriteLine("Starting Verification of urls with UseUniqueServiceUrl option when endpoint ref is provided");
            var context = this.Listener.ServiceContext;
            var expectedUrlToBuildFunc = string.Format("{0}://+:{1}", expectedProtocol, expectedPort);
            var expectedUrlFromOpen = string.Format("{0}://{1}:{2}/{3}/{4}", expectedProtocol, context.NodeContext.IPAddressOrFQDN, expectedPort, context.PartitionId, context.ReplicaOrInstanceId);
            var actualUrlFromOpen = this.Listener.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();

            this.UrlFromListenerToBuildFunc.Should().Be(expectedUrlToBuildFunc, "listener generates the url.");
            actualUrlFromOpen.Should().Be(actualUrlFromOpen, "url from OpenAsync with endpoint ref");
            Console.WriteLine("Completed Verification of urls with endpoint ref");
        }

        /// <summary>
        /// Tests Url for ServiceFabricIntegrationOptions.None
        /// 1. When endpoint name is provided (protocol and port comes from endpoint.) :
        ///   a. url given to Func to create IWebHost should be protocol://+:port. 
        ///   b. url returned from OpenAsync should be protocol://IPAddressOrFQDN:port
        /// 
        /// </summary>        
        protected void WithoutUseUniqueServiceUrlOptionVerifier()
        {
            this.IntegrationOptions = ServiceFabricIntegrationOptions.None;
            var expectedProtocol = ExpectedProtocolFromEndpoint.ToString().ToLower();
            var expectedPort = DefaultExpectedPort;

            Console.WriteLine("Starting Verification of urls with UseUniqueServiceUrl option when endpoint ref is provided");
            var context = this.Listener.ServiceContext;
            var expectedUrlToBuildFunc = string.Format("{0}://+:{1}", expectedProtocol, expectedPort);
            var expectedUrlFromOpen = string.Format("{0}://{1}:{2}", expectedProtocol, context.NodeContext.IPAddressOrFQDN, expectedPort);
            var actualUrlFromOpen = this.Listener.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();

            this.UrlFromListenerToBuildFunc.Should().Be(expectedUrlToBuildFunc, "url to Build Func is generated by listener");
            actualUrlFromOpen.Should().Be(expectedUrlFromOpen, "url from OpenAsync with endpoint ref");
            Console.WriteLine("Completed Verification of urls with endpoint ref");
        }

        /// <summary>
        /// Verify Listener Open and Close.
        /// </summary>        
        protected void ListenerOpenCloseVerifier()
        {
            // Open Close
            this.Listener.OpenAsync(CancellationToken.None).GetAwaiter().GetResult(); ;
            this.IsStarted.Should().BeTrue();

            this.Listener.CloseAsync(CancellationToken.None);
            this.IsStarted.Should().BeFalse();

            // Open Abort
            this.Listener.OpenAsync(CancellationToken.None);
            this.IsStarted.Should().BeTrue();

            this.Listener.Abort();
            this.IsStarted.Should().BeFalse();
        }

        /// <summary>
        /// InvalidOperationException is thrown when Endpoint is not found in service manifest.
        /// </summary>
        protected void ExceptionForEndpointNotFoundVerifier()
        {
            Action action = () =>this.Listener.OpenAsync(CancellationToken.None);
            action.ShouldThrow<InvalidOperationException>();
        }
    }
}
