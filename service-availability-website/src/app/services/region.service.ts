import { Injectable } from '@angular/core';
import { AwsRegion } from './region';

export interface IRegionService {
    getPublicRegions(): Promise<AwsRegion[]>;
    getGovCloudRegions(): Promise<AwsRegion[]>;
    getChinaRegions(): Promise<AwsRegion[]>;
    getAllRegions(): Promise<AwsRegion[]>;
}

@Injectable()
export class RegionService implements IRegionService {
    constructor() { }

    private govCloudRegions: AwsRegion[] = [
        { name: "AWS GovCloud (US-West)", code: "us-gov-west-1" },
        { name: "AWS GovCloud (US-East)", code: "us-gov-east-1" }
    ];

    private chinaRegions: AwsRegion[] = [
        { name: "China (Beijing)", code: "cn-north-1" },
        { name: "China (Ningxia)", code: "cn-northwest-1" }
    ];

    private publicRegions: AwsRegion[] = [
        { name: "US East (N. Virginia)", code: "us-east-1" },
        { name: "US East (Ohio)", code: "us-east-2" },
        { name: "US West (N. California)", code: "us-west-1" },
        { name: "US West (Oregon)", code: "us-west-2" },
        { name: "Asia Pacific (Mumbai)", code: "ap-south-1" },
        { name: "Asia Pacific (Tokyo)", code: "ap-northeast-1" },
        { name: "Asia Pacific (Seoul)", code: "ap-northeast-2" },
        { name: "Asia Pacific (Osaka-Local)", code: "ap-northeast-3" },
        { name: "Asia Pacific (Singapore)", code: "ap-southeast-1" },
        { name: "Asia Pacific (Sydney)", code: "ap-southeast-2" },
        { name: "Asia Pacific (Hong Kong)", code: "ap-east-1" },
        { name: "Canada (Central)", code: "ca-central-1" },
        { name: "Europe (Frankfurt)", code: "eu-central-1" },
        { name: "Europe (Ireland)", code: "eu-west-1" },
        { name: "Europe (London)", code: "eu-west-2" },
        { name: "Europe (Paris)", code: "eu-west-3" },
      { name: "Europe (Stockholm)", code: "eu-north-1" },
      { name: "Europe (Milan)", code: "eu-south-1" },
        { name: "Middle East (Bahrain)", code: "me-sout-1" },
      { name: "South America (São Paulo)", code: "sa-east-1" },
      { name: "Africa (Cape Town)", code: "af-south-1" }
    ];

    public async getChinaRegions(): Promise<AwsRegion[]> {
        return Promise.resolve(this.chinaRegions);
    }

    public async getGovCloudRegions(): Promise<AwsRegion[]> {
        return Promise.resolve(this.govCloudRegions);
    }

    public async getPublicRegions(): Promise<AwsRegion[]> {
        return Promise.resolve(this.publicRegions);
    }

    public async getAllRegions(): Promise<AwsRegion[]> {
        return Promise.resolve(this.publicRegions.concat(this.govCloudRegions, this.chinaRegions));
    }
}
