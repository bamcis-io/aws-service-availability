export class ServiceHealthEntry {
    public Service: string;
    public Date: number;
    public Began: number;
    public Ended: number;
    public ElapsedTime: number;
    public Region: string;
    public Description: string;
    public Summary: string;
    public MonthlyOutageDurations: Map<string, number>;

    constructor() { }
}
