using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BAMCIS.ServiceAvailability.DDBConverters
{
    public class TimelineConverter : IPropertyConverter
    {
        public DynamoDBEntry ToEntry(object value)
        {
            EventTimeline timeline = value as EventTimeline;
            if (timeline == null) throw new ArgumentOutOfRangeException();

            return new Primitive
            {
                Value = JsonConvert.SerializeObject(timeline)
            };
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            Primitive primitive = entry as Primitive;

            if (primitive == null || !(primitive.Value is String) || string.IsNullOrEmpty((string)primitive.Value))
                throw new ArgumentOutOfRangeException();

            string data = (string)primitive;

            return JsonConvert.DeserializeObject<EventTimeline>(data);
        }
    }
}
