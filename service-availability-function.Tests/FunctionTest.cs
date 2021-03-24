using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
using Amazon.Lambda.TestUtilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BAMCIS.ServiceAvailability.Tests
{
    public class FunctionTest
    {
        [Fact]
        public async Task Live_TestScheduledLoadFromSHD()
        {
            // ARRANGE
            Entrypoint ep = new Entrypoint();

            ScheduledEvent request = new ScheduledEvent();

            TestLambdaLogger logger = new TestLambdaLogger();
            TestLambdaContext context = new TestLambdaContext();
            context.Logger = logger;

            // ACT
            await ep.ScheduledLoadDataFromSource(request, context);

            // ASSERT
            Assert.True(true);
        }

        [Fact]
        public async Task Live_TestManualLoadFromSHD()
        {
            // ARRANGE
            Entrypoint ep = new Entrypoint();

            APIGatewayProxyRequest request = new APIGatewayProxyRequest();

            TestLambdaLogger logger = new TestLambdaLogger();
            TestLambdaContext context = new TestLambdaContext();
            context.Logger = logger;

            // ACT
            await ep.ManualLoadDataFromSource(request, context);

            // ASSERT
            Assert.True(true);
        }

        [Fact]
        public async Task Live_TestGetDataAsync()
        {
            // ARRANGE
            Entrypoint ep = new Entrypoint();

            APIGatewayProxyRequest request = new APIGatewayProxyRequest()
            {
                  QueryStringParameters = new Dictionary<string, string>() { {"output", "None" } }
            };

            TestLambdaLogger logger = new TestLambdaLogger();
            TestLambdaContext context = new TestLambdaContext();
            context.Logger = logger;

            // ACT
            APIGatewayProxyResponse response = await ep.GetData(request, context);

            // ASSERT
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task Live_TestGetDataWithFilterAsync()
        {
            // ARRANGE
            Entrypoint ep = new Entrypoint();

            APIGatewayProxyRequest request = new APIGatewayProxyRequest()
            {
                QueryStringParameters = new Dictionary<string, string>() { { "output", "None" }, { "services", "ec2,autoscaling,awswaf" } }
            };

            TestLambdaLogger logger = new TestLambdaLogger();
            TestLambdaContext context = new TestLambdaContext();
            context.Logger = logger;

            // ACT
            APIGatewayProxyResponse response = await ep.GetData(request, context);

            // ASSERT
            Assert.Equal(200, response.StatusCode);
            Assert.True(!String.IsNullOrEmpty(response.Body));
        }

        [Fact]
        public void ComplexBetweenXandY()
        {
            // ARRANGE
            // Wed Aug 08 2018 10:02:04 GMT+0000
            // Between 5:10 PM on August 7th, and 3:50 AM PDT on August 8th, 7 hours difference
            DateTime start = new DateTime(2018, 8, 8, 0, 10, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2018, 8, 8, 10, 50, 0, DateTimeKind.Utc);

            DashboardEventRaw ev = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("emr-ap-south-1.json"));

            // ACT
            EventTimeline startEnd = EventTimelineUtilities.GetEventTimeline(ev);

            // ASSERT
            Assert.Equal(start, startEnd.Start);
            Assert.Equal(end, startEnd.End);
        }

        [Fact]
        public void SimpleBetweenXandYRegex()
        {
            // ARRANGE
            // Tue May 15 2018 15:12:21 GMT+0000
            // Between 5:27 AM and 8:17 AM PDT, 7 hours difference
            DateTime start = new DateTime(2018, 5, 15, 12, 27, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2018, 5, 15, 15, 17, 0, DateTimeKind.Utc);

            DashboardEventRaw ev = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("ec2-us-east-1.json"));

            // ACT
            EventTimeline startEnd = EventTimelineUtilities.GetEventTimeline(ev);

            // ASSERT
            Assert.Equal(start, startEnd.Start);
            Assert.Equal(end, startEnd.End);
        }

        [Fact]
        public void TestUpdateSplit()
        {
            // ARRANGE
            string test2 = @"
<div>
<span class=""yellowfg""> 3:13 PM PDT</span>
&nbsp;We are investigating connectivity issues affecting some instances in a single Availability Zone in the US-EAST-1 Region.\r\n
</div>

<div>
<span class=""yellowfg""> 3:42 PM PDT</span>
&nbsp;We can confirm that there has been an issue in one of the datacenters that makes up one of US-EAST-1 Availability Zones. This was a result of a power event impacting a small percentage of the physical servers in that datacenter as well as some of the networking devices. Customers with EC2 instances in this availability zone may see issues with connectivity to the affected instances. We are seeing recovery and continue to work toward full resolution.
</div>

<div><span class=""yellowfg""> 4:29 PM PDT</span>
&nbsp;We have restored power to the vast majority of the affected instances and continue to work towards full recovery.
</div>

<div>
<span class=""yellowfg""> 5:36 PM PDT</span>
&nbsp;﻿﻿Beginning at 2:52 PM PDT a small percentage of EC2 servers lost power in a single Availability Zone in the US-EAST-1 Region. This resulted in some impaired EC2 instances and degraded performance for some EBS volumes in the affected Availability Zone. Power was restored at 3:22 PM PDT, at which point the vast majority of instances and volumes saw recovery. We have been working to recover the remaining instances and volumes. The small number of remaining instances and volumes are hosted on hardware which was adversely affected by the loss of power. While we will continue to work to recover all affected instances and volumes, for immediate recovery, we recommend replacing any remaining affected instances or volumes if possible.
</div>";

            string test = "<div><span class=\"yellowfg\"> 3:13 PM PDT</span>&nbsp;We are investigating connectivity issues affecting some instances in a single Availability Zone in the US-EAST-1 Region.\r\n</div><div><span class=\"yellowfg\"> 3:42 PM PDT</span>&nbsp;We can confirm that there has been an issue in one of the datacenters that makes up one of US-EAST-1 Availability Zones. This was a result of a power event impacting a small percentage of the physical servers in that datacenter as well as some of the networking devices. Customers with EC2 instances in this availability zone may see issues with connectivity to the affected instances. We are seeing recovery and continue to work toward full resolution.</div><div><span class=\"yellowfg\"> 4:29 PM PDT</span>&nbsp;We have restored power to the vast majority of the affected instances and continue to work towards full recovery.</div><div><span class=\"yellowfg\"> 5:36 PM PDT</span>&nbsp;﻿﻿Beginning at 2:52 PM PDT a small percentage of EC2 servers lost power in a single Availability Zone in the US-EAST-1 Region. This resulted in some impaired EC2 instances and degraded performance for some EBS volumes in the affected Availability Zone. Power was restored at 3:22 PM PDT, at which point the vast majority of instances and volumes saw recovery. We have been working to recover the remaining instances and volumes. The small number of remaining instances and volumes are hosted on hardware which was adversely affected by the loss of power. While we will continue to work to recover all affected instances and volumes, for immediate recovery, we recommend replacing any remaining affected instances or volumes if possible.</div>";

            // ACT
            Dictionary<string, string> dict = EventTimelineUtilities.SplitUpdates(test);

            // ASSERT
            Assert.Equal(4, dict.Count);
        }

        [Fact]
        public void ReplaceTimeZoneTest()
        {
            // ARRANGE
            string date = "May 5, 5:45 AM PST";

            // ACT
            date = EventTimelineUtilities.ReplaceTimeZoneWithOffset(date);

            // ASSERT
            Assert.Equal("May 5, 5:45 AM -08:00", date);
        }

        [Fact]
        public void DateMismatchTest()
        {
            // ARRANGE
            DashboardEventRaw ev = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("date_mismatch.json"));
            DateTime dt = ServiceUtilities.ConvertFromUnixTimestamp(Int64.Parse(ev.Date));

            // ACT
            DateTime baseDate = EventTimelineUtilities.GetBaseDate(ev);

            // ASSERT
            Assert.Equal(new DateTime(2019, 5, 10, 0, 0, 0, DateTimeKind.Utc), baseDate);
            Assert.NotEqual(dt.Day, baseDate.Day);
        }

        [Fact]
        public void DateNoMismatchTest()
        {
            // ARRANGE
            DashboardEventRaw ev = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("date_no_mismatch.json"));

            // ACT
            DateTime baseDate = EventTimelineUtilities.GetBaseDate(ev);

            // ASSERT
            Assert.Equal(new DateTime(2019, 5, 10, 0, 0, 0, DateTimeKind.Utc), baseDate);
        }

        [Fact]
        public void TestAllData()
        {
            // ARRANGE
            DashboardEventData data = JsonConvert.DeserializeObject<DashboardEventData>(File.ReadAllText("data.json"));

            File.WriteAllText("misses.json", "[");

            int success = 0;
            int unhandled = 0;

            List<EventTimeline> timelines = new List<EventTimeline>();

            // ACT
            foreach (DashboardEventRaw ev in data.Archive)
            {
                try
                {
                    EventTimeline evTimeline = EventTimelineUtilities.GetEventTimeline(ev);
                    timelines.Add(evTimeline);
                    success += 1;

                    if (evTimeline.EndTimeWasFoundInDescription == false)
                    {
                        File.AppendAllText("misses.json", JsonConvert.SerializeObject(ev));
                    }
                }
                catch (Exception e)
                {
                    unhandled += 1;
                }
            }

            File.AppendAllText("misses.json", "]");

            // ASSERT
            Assert.Equal(data.Archive.Count(), success);
            Assert.Equal(0, unhandled);
        }

        [Fact]
        public void TestEvent1()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-1.json"));

            // ACT
            EventTimeline startEnd = EventTimelineUtilities.GetEventTimeline(data);

            // ASSERT
            Assert.Equal(new DateTime(2020, 11, 10, 19, 59, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2020, 11, 11, 2, 25, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent1WithParse()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-1.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);
            EventTimeline startEnd = parsed.Timeline;

            // ASSERT
            Assert.Equal(new DateTime(2020, 11, 10, 19, 59, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2020, 11, 11, 2, 25, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent2()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-2.json"));

            // ACT
            EventTimeline startEnd = EventTimelineUtilities.GetEventTimeline(data);

            // ASSERT
            Assert.Equal(new DateTime(2020, 12, 7, 7, 10, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2020, 12, 7, 13, 45, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent3()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-3.json"));

            // ACT
            EventTimeline startEnd = EventTimelineUtilities.GetEventTimeline(data);

            // ASSERT
            Assert.Equal(new DateTime(2019, 4, 25, 16, 11, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2019, 4, 25, 19, 13, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent4()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-4.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);

            // ASSERT
            Assert.NotNull(parsed.Timeline);
        }

        [Fact]
        public void TestEvent5WithParse()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-5.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);
            EventTimeline startEnd = parsed.Timeline;

            // ASSERT
            Assert.Equal(new DateTime(2017, 2, 28, 17, 37, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2017, 2, 28, 23, 48, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent6WithParse()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-6.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);
            EventTimeline startEnd = parsed.Timeline;

            // ASSERT
            Assert.Equal(new DateTime(2016, 8, 16, 17, 18, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2016, 8, 16, 17, 28, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent7WithParse()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-7.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);
            EventTimeline startEnd = parsed.Timeline;

            // ASSERT
            Assert.Equal(new DateTime(2016, 9, 14, 6, 55, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2016, 9, 14, 8, 3, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent8WithParse()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-8.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);
            EventTimeline startEnd = parsed.Timeline;

            // ASSERT
            Assert.Equal(new DateTime(2016, 6, 5, 5, 25, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2016, 6, 5, 7, 45, 0, DateTimeKind.Utc), startEnd.End);
        }

        [Fact]
        public void TestEvent9WithParse()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-9.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);
            EventTimeline startEnd = parsed.Timeline;

            // ASSERT
            Assert.Equal(default(DateTime), startEnd.Start);
            Assert.Equal(default(DateTime), startEnd.End);
        }

        [Fact]
        public void TestEvent10WithParse()
        {
            // ARRANGE
            DashboardEventRaw data = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("test-event-10.json"));

            // ACT
            DashboardEventParsed parsed = DashboardEventParsed.FromRawEvent(data);
            EventTimeline startEnd = parsed.Timeline;

            // ASSERT
            Assert.Equal(new DateTime(2015, 11, 6, 5, 30, 0, DateTimeKind.Utc), startEnd.Start);
            Assert.Equal(new DateTime(2015, 11, 6, 10, 10, 0, DateTimeKind.Utc), startEnd.End);
        }
    }
}
