using Amazon.DynamoDBv2.DocumentModel;
using BAMCIS.ServiceAvailability.DDBConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BAMCIS.ServiceAvailability.Tests
{
    public class DDBConverterTests
    {
        [Fact]
        public void TestEnumConverterFrom()
        {
            // ARRANGE
            DashboardEventStatus status = DashboardEventStatus.BLUE;
            EnumConverter<DashboardEventStatus> converter = new DDBConverters.EnumConverter<DashboardEventStatus>();
            DynamoDBEntry entry = new Primitive() { Value = status.ToString() };

            // ACT

            object data = converter.FromEntry(entry);
            DashboardEventStatus derived = (DashboardEventStatus)data;

            // ASSERT
            Assert.Equal(DashboardEventStatus.BLUE, derived);
        }

        [Fact]
        public void TestEnumConverterTo()
        {
            // ARRANGE
            DashboardEventStatus status = DashboardEventStatus.BLUE;
            EnumConverter<DashboardEventStatus> converter = new DDBConverters.EnumConverter<DashboardEventStatus>();
            
            // ACT
            DynamoDBEntry entry = converter.ToEntry(status);

            // ASSERT
            Assert.Equal("BLUE", entry.AsString());
        }

        [Fact]
        public void TestDictionaryConverterFrom()
        {
            // ARRANGE
            Dictionary<string, long> dict = new Dictionary<string, long>() { { "1", 2 }, { "3", 4 } };
            DictionaryConverter<string, long> converter = new DictionaryConverter<string, long>();
            DynamoDBEntry entry = new Primitive() { Value = JsonConvert.SerializeObject(dict) };

            // ACT

            object data = converter.FromEntry(entry);
            Dictionary<string, long> derived = (Dictionary<string, long>)data;

            // ASSERT
            Assert.Equal(dict, derived);
        }

        [Fact]
        public void TestDictionaryConverterTo()
        {
            // ARRANGE
            Dictionary<string, long> dict = new Dictionary<string, long>() { { "1", 2 }, { "3", 4 } };
            DictionaryConverter<string, long> converter = new DictionaryConverter<string, long>();

            // ACT
            DynamoDBEntry entry = converter.ToEntry(dict);

            // ASSERT
            Assert.Equal("{\"1\":2,\"3\":4}", entry.AsString());
        }

        [Fact]
        public void TestDateTimeConverterFrom()
        {
            // ARRANGE
            DateTime date = DateTime.UtcNow;
            date = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, date.Kind);
            TimestampConverter converter = new TimestampConverter();
            DynamoDBEntry entry = new Primitive() { Value = date.ToString("o") };

            // ACT

            object data = converter.FromEntry(entry);
            DateTime derived = ServiceUtilities.ConvertFromUnixTimestamp((long)data);

            // ASSERT
            Assert.Equal(date, derived);
        }

        [Fact]
        public void TestDateTimeConverterTo()
        {
            // ARRANGE
            long time = 1525914401;
            DateTime dt = ServiceUtilities.ConvertFromUnixTimestamp(time);
            TimestampConverter converter = new TimestampConverter();

            // ACT
            DynamoDBEntry data = converter.ToEntry(time);

            // ASSERT
            Assert.Equal(dt, DateTime.Parse(data.AsString()).ToUniversalTime());
        }
    }
}
