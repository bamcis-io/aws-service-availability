# AWS Service Availability
This is a serverless application that parses the data found on the [AWS Service Health Dashboard](http://status.aws.amazon.com). It presents a front-end UI hosted in S3 to query the data and determine at a coarse-grained level when there were outages or increased error rates being reported by AWS. This data can then be used to compare with SLA targets as a starting point to investigating whether to apply for SLA credits.

## Table of Contents
- [Prequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Using the API](#using-the-api)
- [Notifications](#notifications)
- [Notes](#notes)
- [Roadmap](#roadmap)
- [Revision History](#revision-history)

## Understanding the Data
As a precusor to using this app, it's important to understand the data. The data is pulled from the AWS service health dashboard and is put through a handful of regex's to determine start and end times of the event in order to calculate the total affected time. However, the notices posted to the SHD are typically not that the service is "down", it's usually that a service is experiencing higher than typical error rates or latency that impact some subset of customers in a specific region. So, the data shouldn't be interpreted as meaning the service was hard down for everyone during the calculated window. It means SOME people MAY have been affected for SPECIFIC actions SOME of the time in a SPECIFIC location. 

## Prequisites
You will need [`node.js`](https://nodejs.org/) and the [`Angular CLI`](https://cli.angular.io/) installed in order to build the website. 

## Getting Started
First, deploy the Serverless Application Model (SAM) template. You can do this via the [SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/serverless-sam-cli-install.html) or directly through Visual Studio with the AWS Toolkit. The CloudFormation template deploys a CloudFront distribution that points to an API Gateway Endpoint that triggers a Lambda function. It also deploys an AWS WAF that restricts the access to the website and API to an IP address you provide.

Once the infrastructure is deployed, go to the folder `service-availability-website` in this solution. Run

```
ng build --prod
```

This will build the website. Once that is complete, upload the contents of the newly created `dist` folder to the root of the S3 bucket that was created by the CloudFormation template. Access the website using the distribution URL provided by Cloudfront, i.e. `https://cf-dist-id.cloudfront.net`. 

## Using the API
You can also access the API directly by using the `/api` path in the Cloudfront distribution, for example:

```
https://cf-dist-id.cloudfront.net/api/serviceavailability
```

Today, there's only 1 path supported by the API Gateway origin behind Cloudfront, `/serviceavailability`, so you must include that as part of the URL path.

There are 5 parameters that the API accepts

- `output` - This can be `none`, `csv`, or `json`. If you specify csv or json, the response provides a downloadable file, otherwise it just returns a JSON text string.
- `start` - The unix timestamp in seconds to filter only events that were reported after this time (inclusive of the timestamp).
- `end` - The unix timestamp in seconds to filter only events that were reported before this time (inclusive of the timestamp).
- `services` - The services to include in the results, a comma delimited list, like `ec2`,`s3`,`lambda`. Omit this parameter to include all services.
- `regions` - The regions to include in the results, a comma delimited list, like `us-east-1`, `us-east-2`, `eu-west-1`. Omit this parameter to include all regions.

An example of using the parameters
```
https://cf-dist-id.cloudfront.net/api/serviceavailability?output=csv&regions=us-east-1&services=ec2,s3&start=1546300800&end=1577836799
```
This retrieves all events that happened during the year 2019 (2019-01-01T00:00:00+00:00 to 2019-12-31T23:59:59+00:00) for EC2 and S3 in us-east-1. The data is returned as a CSV file.

## Notifications
Since the logic is based on a series of regex's, it's possible the format of the description may change in the future and cause the regex to not match the contents. The app will send SNS notifications (if configured) any time this occurs. There is no circuit breaker for this, so you'll get one each time the function is invoked, thus be cautious. You may instead want to log these events into a separate CloudWatch log stream and monitor that for log entries. 

## Notes
In the UI, the set of regions is static, so if new regions are released, that list will need to be updated and the site will need to be rebuilt. The same is true of the list of services.

The default output is JSON, but you must specify JSON as an output if you want the HTTP response to present a file to be downloaded.

## Roadmap
The following are items that I might implement.

1) Pulling runtime configuration data from SSM Parameter Store. The CloudFormation populates the parameter already, but the code logic doesn't pull from them. Today the function uses a set of defaults which are adequate for all of the currently existing alerts (and are mirrored in the SSM Parameters).
2) Unit tests.
3) Migrate API to OpenAPI 3.0 format.
4) Send missed regex matches to CloudWatch instead of SNS.

## Revision History

### 1.0.0-beta
Initial beta release of the application.