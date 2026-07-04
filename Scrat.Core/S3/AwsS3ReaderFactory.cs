using Amazon.Runtime;
using Amazon.S3;
using Scrat.Core.Configuration;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>Builds AWS SDK clients from endpoint configuration.</summary>
public sealed class AwsS3ReaderFactory : IS3ReaderFactory
{
    public IS3Reader Create(S3EndpointConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var s3Config = new AmazonS3Config { ForcePathStyle = true };
        if (!string.IsNullOrWhiteSpace(config.ServiceUrl))
        {
            s3Config.ServiceURL = config.ServiceUrl;
            s3Config.AuthenticationRegion = config.Region;
        }
        else if (!string.IsNullOrWhiteSpace(config.Region))
        {
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(config.Region);
        }
        else
        {
            throw new InvalidOperationException(
                "S3 endpoint configuration must set 'ServiceUrl' (S3-compatible cluster) or 'Region' (AWS). See the 'S3Endpoints' section.");
        }

        var credentials = new BasicAWSCredentials(config.AccessKey, config.SecretKey);
        return new AwsS3Reader(new AmazonS3Client(credentials, s3Config));
    }
}
