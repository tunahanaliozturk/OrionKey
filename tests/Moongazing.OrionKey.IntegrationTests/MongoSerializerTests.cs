using System.IO;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Moongazing.OrionKey.IntegrationTests;

public class MongoSerializerTests
{
    static MongoSerializerTests()
    {
        // MongoDB 3.x removed BsonDefaults.GuidRepresentation and now defaults to
        // GuidRepresentation.Unspecified, which throws when serializing System.Guid.
        // Register a GuidSerializer with Standard representation (RFC 4122) so the
        // BsonSerializer.LookupSerializer<Guid>() call in the generated serializer
        // returns one that can encode/decode without error.
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
            // Already registered — OK if multiple test classes do this.
        }
    }

    private static byte[] SerializeToBson<T>(IBsonSerializer<T> serializer, T value)
    {
        using var stream = new MemoryStream();
        using var writer = new BsonBinaryWriter(stream);
        writer.WriteStartDocument();
        writer.WriteName("v");
        var ctx = BsonSerializationContext.CreateRoot(writer);
        serializer.Serialize(ctx, new BsonSerializationArgs { NominalType = typeof(T) }, value);
        writer.WriteEndDocument();
        return stream.ToArray();
    }

    private static T DeserializeFromBson<T>(IBsonSerializer<T> serializer, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BsonBinaryReader(stream);
        reader.ReadStartDocument();
        reader.ReadName();
        var ctx = BsonDeserializationContext.CreateRoot(reader);
        var value = serializer.Deserialize(ctx, new BsonDeserializationArgs { NominalType = typeof(T) });
        reader.ReadEndDocument();
        return value;
    }

    [Fact]
    public void OrderId_Guid_RoundTripsThroughBson()
    {
        var serializer = new OrderIdBsonSerializer();
        var id = OrderId.New();
        var bytes = SerializeToBson<OrderId>(serializer, id);
        var restored = DeserializeFromBson<OrderId>(serializer, bytes);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void TenantId_Ulid_RoundTripsThroughBson()
    {
        var serializer = new TenantIdBsonSerializer();
        var id = TenantId.New();
        var bytes = SerializeToBson<TenantId>(serializer, id);
        var restored = DeserializeFromBson<TenantId>(serializer, bytes);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void UserId_Snowflake_RoundTripsThroughBson()
    {
        var serializer = new UserIdBsonSerializer();
        var id = new UserId(987654321);
        var bytes = SerializeToBson<UserId>(serializer, id);
        var restored = DeserializeFromBson<UserId>(serializer, bytes);
        Assert.Equal(id, restored);
    }
}
