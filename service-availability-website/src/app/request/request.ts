import { IRequestService } from '../services/request.service';
import { Injectable, Inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable()
export class Request {
    private url: string = "https://15vcawha7k.execute-api.us-east-1.amazonaws.com/Prod/api/serviceavailability";
    
    public services: string;
    public regions: string;
    public start: Date;
    public end: Date;
    public output: string;

    constructor(@Inject("IRequestService") private _requestService: IRequestService) {
        this.services = "All";
        this.regions = "All";
        this.output = "None";
    };

    public GetUrl(): string {
        return this.url;
    }

    public Submit(): Observable<HttpResponse<any>> {
        return this._requestService.Submit(this);
    }   
}
