using DeviceSimulationApp.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace DeviceSimulationApp
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// Default URL for triggering event grid function in the local environment.
    /// http://localhost:7071/runtime/webhooks/EventGridExtensionConfig?functionName=SendEventConsumer
    /// </remarks>
    public static class SendEventConsumer
    {
        [FunctionName("SendEventConsumer")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, TraceWriter log)
        {
            var connectedDeviceItem = JsonConvert.DeserializeObject<ConnectedDeviceItem>(eventGridEvent.Data.ToString());
            var deviceClient = DeviceClient.CreateFromConnectionString(connectedDeviceItem.DeviceConnectionString);

            var message = CreateMessageBody(connectedDeviceItem);
            await deviceClient.SendEventAsync(message);

            var jsonObject = JObject.Parse(connectedDeviceItem.InitialState);
            var properties = jsonObject.Children<JObject>().Properties();
            foreach (var property in properties)
            {
                if (property.Name == "temperature")
                {

                }
            }

            await Task.Delay(connectedDeviceItem.Interval * 1000);
            var sendEventGridEvent = new EventGridEvent()
            {
                Id = Guid.NewGuid().ToString(),
                EventTime = DateTime.UtcNow,
                Data = JsonConvert.SerializeObject(connectedDeviceItem),
                EventType = "sendEvent",
                Subject = "simulation/devices/event",
                DataVersion = "1.0",
            };

            await SendEventGridEvents(new List<EventGridEvent>(1) { sendEventGridEvent });
        }

        private static Message CreateMessageBody(ConnectedDeviceItem connectedDeviceItem)
        {
            var eventJson = JsonConvert.SerializeObject(connectedDeviceItem.InitialState);
            var eventJsonBytes = Encoding.UTF8.GetBytes(eventJson);
            var message = new Message(eventJsonBytes);

            IDictionary<string, string> messageProperties = message.Properties;
            messageProperties.Add("messageType", connectedDeviceItem.MessageType);
            messageProperties.Add("correlationId", Guid.NewGuid().ToString());
            messageProperties.Add("parentCorrelationId", Guid.NewGuid().ToString());
            messageProperties.Add("createdDateTime", DateTime.UtcNow.ToString("u", DateTimeFormatInfo.InvariantInfo));
            messageProperties.Add("deviceId", connectedDeviceItem.Name);

            var properties = connectedDeviceItem.Properties;
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    messageProperties.Add(property.Key, property.Value);
                }
            }

            return message;
        }

        private static async Task SendEventGridEvents(IList<EventGridEvent> eventGridEvents)
        {
            var topicEndpoint = Environment.GetEnvironmentVariable("TopicEndpoint");
            var topicHostName = new Uri(topicEndpoint).Host;
            var topicKey = Environment.GetEnvironmentVariable("TopicKey");
            var topicCredentials = new TopicCredentials(topicKey);
            var eventGridClient = new EventGridClient(topicCredentials);

            await eventGridClient.PublishEventsAsync(topicHostName, eventGridEvents);
        }
    }
}
