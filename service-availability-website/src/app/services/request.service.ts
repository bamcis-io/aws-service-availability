import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Request } from "../request/request";
import { Observable } from 'rxjs';


export interface IRequestService {
    Submit(request: Request): Observable<HttpResponse<string>>
}

@Injectable()
export class RequestService implements IRequestService {

    constructor(private http: HttpClient) {
    };

    public Submit(request: Request): Observable<HttpResponse<string>> {
        let headers = new HttpHeaders();
        let params = new HttpParams();

        params = params.append("output", request.output);
        headers = headers.append("Accept", "*/*");

        if (request.services !== undefined && request.services !== null &&
            request.services.indexOf("All") === -1 && request.services.length > 0) {
            params = params.append("services", request.services);
        }

        if (request.regions !== undefined && request.regions !== null &&
            request.regions.indexOf("All") === -1 && request.regions.length > 0) {
            params = params.append("regions", request.regions);
        }

        // Javascript believes the start and end properties are strings instead of Date objects
        // We're setting the start and end to the very beginning and very end of the day to ensure
        // the selected date is inclusive of all events on that day
        if (request.start !== undefined && request.start !== null) {
            let start: Date = new Date(request.start.toString());
            start.setHours(0, 0, 0, 0);

            start = this.ConvertToUtc(start);
            
            params = params.append("start", Math.round(start.getTime() / 1000).toString());
        }

        if (request.end !== undefined && request.end !== null) {
            let end: Date = new Date(request.end.toString());
            // Don't set milliseconds since this will round up to the next second
            end.setHours(23, 59, 59);

            end = this.ConvertToUtc(end);

            params = params.append("end", Math.round(end.getTime() / 1000).toString());
        }

        return this.http.get(request.GetUrl(), {
            params: params,
            headers: headers,
            responseType: 'text',
            observe: 'response'
        });
    }

    private ConvertToUtc(date: Date): Date {
        // The time-zone offset is the difference, in minutes, between UTC and local time. 
        // Note that this means that the offset is positive if the local timezone is behind UTC and negative if it is ahead.
        let offset: number = date.getTimezoneOffset() * -1;

        // Convert the time to UTC
        date = new Date(date.toUTCString());

        // Add the milliseconds of the offset to the UTC time
        // This gives us midnight of the selected day in UTC
        date = new Date(date.getTime() + (offset * 60 * 1000));

        return date;
    }
}
