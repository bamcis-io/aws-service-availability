using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BAMCIS.ServiceAvailability.DDBConverters
{
    public class DictionaryConverter<T, U> : IPropertyConverter
    {
        public DynamoDBEntry ToEntry(object value)
        {
            Dictionary<T, U> dict = value as Dictionary<T, U>;
            if (dict == null) throw new ArgumentOutOfRangeException();

            return new Primitive
            {
                Value = JsonConvert.SerializeObject(dict)
            };
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            Primitive primitive = entry as Primitive;

            if (primitive == null || !(primitive.Value is String) || string.IsNullOrEmpty((string)primitive.Value))
                throw new ArgumentOutOfRangeException();

            string data = (string)primitive;

            return JsonConvert.DeserializeObject<Dictionary<T, U>>(data);
        }
    }
}
