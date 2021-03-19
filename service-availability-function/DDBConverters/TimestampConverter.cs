using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;

namespace BAMCIS.ServiceAvailability.DDBConverters
{
    public class TimestampConverter : IPropertyConverter
    {
        public DynamoDBEntry ToEntry(object value)
        {
            long num = (long)value;
            
            return new Primitive
            {
                Value = ServiceUtilities.ConvertFromUnixTimestamp(num).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            Primitive primitive = entry as Primitive;

            if (primitive == null || !(primitive.Value is string) || string.IsNullOrEmpty((string)primitive.Value))
                throw new ArgumentOutOfRangeException();

            DateTime data = DateTime.Parse((string)primitive.Value).ToUniversalTime();

            return ServiceUtilities.ConvertToUnixTimestamp(data);
        }
    }
}
