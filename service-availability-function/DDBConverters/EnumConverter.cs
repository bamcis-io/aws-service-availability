using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;

namespace BAMCIS.ServiceAvailability.DDBConverters
{
    public class EnumConverter<T> : IPropertyConverter where T : Enum
    {
        public DynamoDBEntry ToEntry(object value)
        {
            T enumVal = (T)value;
            if (enumVal == null) throw new ArgumentOutOfRangeException();

            return new Primitive
            {
                Value = enumVal.ToString()
            };
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            Primitive primitive = entry as Primitive;

            if (primitive == null || !(primitive.Value is String) || string.IsNullOrEmpty((string)primitive.Value))
                throw new ArgumentOutOfRangeException();

            string data = (string)primitive;

            return (T)Enum.Parse(typeof(T), data);
        }
    }
}
