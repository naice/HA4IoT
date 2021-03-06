﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using HA4IoT.Contracts.Logging;

namespace HA4IoT.Api.AzureCloud
{
    public class QueueReceiver
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly Uri _uri;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private bool _wasStarted;

        public QueueReceiver(string namespaceName, string queueName, string authorization, TimeSpan timeout)
        {
            if (namespaceName == null) throw new ArgumentNullException(nameof(namespaceName));
            if (queueName == null) throw new ArgumentNullException(nameof(queueName));
            if (authorization == null) throw new ArgumentNullException(nameof(authorization));

            _uri = new Uri($"https://{namespaceName}.servicebus.windows.net/{queueName}/messages/head?api-version=2015-01&timeout={(int)timeout.TotalSeconds}");

            _httpClient.DefaultRequestHeaders.TryAppendWithoutValidation("Authorization", authorization);
        }

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public void Start()
        {
            if (_wasStarted)
            {
                throw new InvalidOperationException("Starting multiple times is not allowed.");    
            }

            _wasStarted = true;

            var task = Task.Factory.StartNew(
                WaitForMessages,
                _cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            task.ConfigureAwait(false);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        private void WaitForMessages()
        {
            Log.Verbose("Started waiting for messages on Azure queue.");
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    WaitForMessage();
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Error while waiting for message.");
                }
            }
        }

        private void WaitForMessage()
        {
            // DELETE will force a "Receive & Delete".
            // POST will force a "Peek-Lock"
            HttpResponseMessage result = _httpClient.DeleteAsync(_uri).AsTask().Result;
            if (result.StatusCode == HttpStatusCode.NoContent)
            {
                Log.Verbose("Azure queue timeout reached. Reconnecting...");
                return;
            }

            if (result.IsSuccessStatusCode)
            {
                var task = Task.Factory.StartNew(
                    async () => await HandleQueueMessage(result.Headers, result.Content),
                    _cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning, 
                    TaskScheduler.Default);

                task.ConfigureAwait(false);
            }
            else
            {
                Log.Warning($"Failed to wait for Azure queue message (Error code: {result.StatusCode}).");
            }
        }

        private async Task HandleQueueMessage(HttpResponseHeaderCollection headers, IHttpContent content)
        {
            string brokerPropertiesSource;
            if (!headers.TryGetValue("BrokerProperties", out brokerPropertiesSource))
            {
                Log.Warning("Received Azure queue message without broker properties.");
                return;
            }
            
            string bodySource = await content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(bodySource))
            {
                Log.Warning("Received Azure queue message with empty body.");
                return;
            }

            JsonObject brokerProperties;
            if (!JsonObject.TryParse(brokerPropertiesSource, out brokerProperties))
            {
                Log.Warning("Received Azure queue message with invalid broker properties.");
                return;
            }

            JsonObject body;
            if (!JsonObject.TryParse(bodySource, out body))
            {
                Log.Warning("Received Azure queue message with not supported body (JSON expected).");
                return;
            }

            Log.Verbose("Received valid Azure queue message.");
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(brokerProperties, body));
        }
    }
}
