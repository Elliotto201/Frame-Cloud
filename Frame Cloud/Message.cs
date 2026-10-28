using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameCloud
{
    public enum MessageType
    {
        PasswordRequest,
        PasswordResponse,
        ProjectHash
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class MessageTypeAttribute : Attribute
    {
        public MessageType Type { get; }

        public MessageTypeAttribute(MessageType type)
        {
            Type = type;
        }
    }

    public abstract class Message
    {
        [JsonIgnore]
        public abstract MessageType Type { get; }
    }

    public static class MessageSerializer
    {
        private static readonly Dictionary<MessageType, Type> MessageTypes = GetMessageTypes();

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            Converters = { new MessageConverter() }
        };

        private static Dictionary<MessageType, Type> GetMessageTypes()
        {
            Dictionary<MessageType, Type> messageTypes = new Dictionary<MessageType, Type>();

            foreach (Type type in typeof(Message).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(Message).IsAssignableFrom(type))
                {
                    continue;
                }

                MessageTypeAttribute? attribute = type.GetCustomAttributes(typeof(MessageTypeAttribute), false)
                    .Cast<MessageTypeAttribute>()
                    .FirstOrDefault();

                if (attribute == null)
                {
                    continue;
                }

                messageTypes.Add(attribute.Type, type);
            }

            return messageTypes;
        }

        public static string SerializeMessage(Message message)
        {
            JsonElement element = JsonSerializer.SerializeToElement(message, message.GetType(), Options);

            Dictionary<string, JsonElement> properties = new Dictionary<string, JsonElement>();

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name == nameof(Message.Type))
                {
                    continue;
                }

                properties.Add(property.Name, property.Value);
            }

            Utf8JsonWriter writer = new Utf8JsonWriter(new System.IO.MemoryStream());
            writer.Dispose();

            string json = JsonSerializer.Serialize(properties, Options);

            using JsonDocument document = JsonDocument.Parse(json);
            using System.IO.MemoryStream stream = new System.IO.MemoryStream();

            using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(stream))
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WriteString(nameof(Message.Type), message.Type.ToString());

                foreach (KeyValuePair<string, JsonElement> property in properties)
                {
                    jsonWriter.WritePropertyName(property.Key);
                    property.Value.WriteTo(jsonWriter);
                }

                jsonWriter.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        public static T DeserializeMessage<T>(string text) where T : Message
        {
            Message message = JsonSerializer.Deserialize<Message>(text, Options)
                ?? throw new JsonException("Message cannot be null.");

            if (message is not T typedMessage)
            {
                throw new JsonException($"Expected {typeof(T).Name}, but received {message.GetType().Name}.");
            }

            return typedMessage;
        }

        public static MessageType GetMessageType(string text)
        {
            using JsonDocument document = JsonDocument.Parse(text);

            JsonElement root = document.RootElement;

            if (!root.TryGetProperty(nameof(Message.Type), out JsonElement typeProperty))
            {
                throw new JsonException("Missing message Type.");
            }

            if (!Enum.TryParse(typeProperty.GetString(), out MessageType messageType))
            {
                throw new JsonException($"Invalid message type: {typeProperty}.");
            }

            return messageType;
        }

        private sealed class MessageConverter : JsonConverter<Message>
        {
            public override Message Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using JsonDocument document = JsonDocument.ParseValue(ref reader);

                JsonElement root = document.RootElement;

                if (!root.TryGetProperty(nameof(Message.Type), out JsonElement typeProperty))
                {
                    throw new JsonException("Missing message Type.");
                }

                if (!Enum.TryParse(typeProperty.GetString(), out MessageType messageType))
                {
                    throw new JsonException($"Invalid message type: {typeProperty}.");
                }

                if (!MessageTypes.TryGetValue(messageType, out Type? messageClass))
                {
                    throw new JsonException($"Unknown message type: {messageType}.");
                }

                return (Message?)JsonSerializer.Deserialize(root.GetRawText(), messageClass, options)
                    ?? throw new JsonException("Message cannot be null.");
            }

            public override void Write(Utf8JsonWriter writer, Message value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString(nameof(Message.Type), value.Type.ToString());

                JsonElement element = JsonSerializer.SerializeToElement(value, value.GetType(), options);

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Name == nameof(Message.Type))
                    {
                        continue;
                    }

                    property.WriteTo(writer);
                }

                writer.WriteEndObject();
            }
        }
    }
}
