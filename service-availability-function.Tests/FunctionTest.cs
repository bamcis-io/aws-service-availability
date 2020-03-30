using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using System.Threading.Tasks;
using Xunit;

namespace BAMCIS.ServiceAvailability.Tests
{
    public class FunctionTest
    {
        [Fact]
        public async Task RunFunction()
        {
            // ARRANGE
            Entrypoint ep = new Entrypoint();

            APIGatewayProxyRequest request = new APIGatewayProxyRequest();

            TestLambdaLogger logger = new TestLambdaLogger();
            TestLambdaContext context = new TestLambdaContext();
            context.Logger = logger;

            // ACT
            APIGatewayProxyResponse response = await ep.Get(request, context);

            // ASSERT
            Assert.Equal(200, response.StatusCode);
        }
    }
}
