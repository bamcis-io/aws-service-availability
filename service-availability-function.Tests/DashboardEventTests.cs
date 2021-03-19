using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace BAMCIS.ServiceAvailability.Tests
{
    public class DashboardEventTests
    {
        private static DashboardEventData data = JsonConvert.DeserializeObject<DashboardEventData>(File.ReadAllText("data.json"));

        #region GetRegion Tests

        [Fact]
        public void GetRegionNameTestForEC2UsEast1()
        {
            // ARRANGE
            DashboardEventRaw ec2 = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("ec2-us-east-1.json"));

            // ACT
            string region = ec2.GetRegion();

            // ASSERT
            Assert.Equal("us-east-1", region);
        }

        [Fact]
        public void GetRegionNameTestForManagementConsoleUsGovWest1()
        {
            // ARRANGE
            DashboardEventRaw mc = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("management-console-us-gov-west-1.json"));

            // ACT
            string region = mc.GetRegion();

            // ASSERT
            Assert.Equal("us-gov-west-1", region);
        }

        [Fact]
        public void GetRegionNameTestForGlueDataBrewUsEast1()
        {
            // ARRANGE
            DashboardEventRaw glue = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("aws-glue-us-east-1.json"));

            // ACT
            string region = glue.GetRegion();

            // ASSERT
            Assert.Equal("us-east-1", region);
        }

        [Fact]
        public void GetRegionNameTestForS3UsStandard()
        {
            // ARRANGE
            DashboardEventRaw s3 = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("s3-us-standard.json"));

            // ACT
            string region = s3.GetRegion();

            // ASSERT
            Assert.Equal("us-standard", region);
        }

        #endregion

        #region GetServiceShortName Tests

        [Fact]
        public void GetServiceNameTestForEC2UsEast1()
        {
            // ARRANGE
            DashboardEventRaw ec2 = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("ec2-us-east-1.json"));

            // ACT
            string name = ec2.GetServiceShortName();

            // ASSERT
            Assert.Equal("ec2", name);
        }

        [Fact]
        public void GetServiceNameTestForGlueDataBrewUsGovWest1()
        {
            // ARRANGE
            DashboardEventRaw glue = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("aws-glue-us-east-1.json"));

            // ACT
            string name = glue.GetServiceShortName();

            // ASSERT
            Assert.Equal("aws-glue", name);
        }

        [Fact]
        public void GetServiceNameTestForS3UsStandard()
        {
            // ARRANGE
            DashboardEventRaw s3 = JsonConvert.DeserializeObject<DashboardEventRaw>(File.ReadAllText("s3-us-standard.json"));

            // ACT
            string name = s3.GetServiceShortName();

            // ASSERT
            Assert.Equal("s3", name);
        }

        #endregion
    }
}
