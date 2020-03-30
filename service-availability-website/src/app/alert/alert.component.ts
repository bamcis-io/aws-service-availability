
import { fromEvent as observableFromEvent } from 'rxjs';

import { distinctUntilChanged, debounceTime } from 'rxjs/operators';
import { Component, OnInit, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';

import { ServiceHealthEntry } from '../request/servicehealthentry';
import { AlertDataSource } from './alert.datasource';
import { Alert } from './alert';
import { MatTableDataSource } from '@angular/material/table';
import { merge as observableMerge, BehaviorSubject } from 'rxjs';
import { map } from 'rxjs/operators';






@Component({
    selector: 'app-alert',
    styleUrls: [
        './alert.component.css'
    ],
    templateUrl: './alert.component.html'
})
export class AlertComponent implements OnInit, AfterViewInit {

    displayedColumns: string[] = [
        "Index",
        "Status",
        "Service",
        "Region",
        "Year",
        "Month",
        "TotalDowntime",
        "Availability"
    ];

    public alertDataSource: MatTableDataSource<Alert>;

    @ViewChild(MatSort) sort: MatSort;
    @ViewChild(MatPaginator) paginator: MatPaginator;
    @ViewChild('filter') filter: ElementRef;

    constructor( ) {
    }

    ngOnInit() {
        this.alertDataSource = new MatTableDataSource<Alert>();
    }

    ngAfterViewInit() {
        this.setupDataSource();
    }

    private setupDataSource() {
        this.alertDataSource.paginator = this.paginator;
        this.alertDataSource.sort = this.sort;
        this.alertDataSource.filterPredicate = (data: Alert, filter: string) => {
            return data.service.indexOf(filter) !== -1;
        };

        observableFromEvent(this.filter.nativeElement, 'keyup').pipe(
            debounceTime(150),
            distinctUntilChanged())
            .subscribe(() => {
                if (!this.alertDataSource) {
                    return;
                }
                this.alertDataSource.filter = this.filter.nativeElement.value;
                this.alertDataSource.paginator.firstPage();
            });

        // reset paginator after sorting
        this.sort.sortChange.subscribe(() => this.paginator.pageIndex = 0);

        let displayDataChanges: any = [
            this.alertDataSource.paginator.page,
            this.alertDataSource.sort.sortChange
        ];

        observableMerge(...displayDataChanges).pipe(map(() => {

            if (this.alertDataSource.data !== undefined && this.alertDataSource.data !== null) {
                let startIndex = this.alertDataSource.paginator.pageIndex * this.alertDataSource.paginator.pageSize;
                let pageSize = this.alertDataSource.paginator.pageSize;

                let temp: Alert[] = this.getSortedData(this.alertDataSource.data);

                temp = this.alertDataSource.data.filter((item: Alert) => {
                    let searchStr = item.service;
                    return searchStr.indexOf(this.alertDataSource.filter) !== -1;
                });

                temp = temp.splice(startIndex, pageSize);

                return temp;
            }
            else {
                return new Array<ServiceHealthEntry>();
            }
        })).subscribe();
    }

    private getSortedData(data: Alert[]): Alert[] {

        if (this.sort === undefined || !this.sort.active || this.sort.direction == '') {
            return data;
        }
        else {
            return data.sort((a, b) => {
                let propertyA: number | string = '';
                let propertyB: number | string = '';

                switch (this.sort.active) {
                    case "Service": {
                        [propertyA, propertyB] = [a.service, b.service];
                        break;
                    }
                    case "Region": {
                        [propertyA, propertyB] = [a.region, b.region];
                        break;
                    }
                    case "Month": {
                        [propertyA, propertyB] = [a.month, b.month];
                        break;
                    }
                    case "Year": {
                        [propertyA, propertyB] = [a.year, b.year];
                        break;
                    }
                    case "TotalDowntime": {
                        [propertyA, propertyB] = [a.totalDownTime, b.totalDownTime];
                        break;
                    }
                    case "Availability": {
                        [propertyA, propertyB] = [a.availability, b.availability];
                        break;
                    }
                }

                let valueA = isNaN(+propertyA) ? propertyA : +propertyA;
                let valueB = isNaN(+propertyB) ? propertyB : +propertyB;

                if (valueA === valueB) {
                    return 0;
                }
                else {
                    return (valueA < valueB ? -1 : 1) * (this.sort.direction == 'asc' ? 1 : -1);
                }
            });
        }
    }

    public buildAlerts(results: ServiceHealthEntry[]) {
        let Results: Map<string, Map<string, Map<string, number>>> = new Map<string, Map<string, Map<string, number>>>();

        let serviceGrouping = this.groupBy(results, result => result.Service);

        let alerts: Alert[] = [];

        serviceGrouping.forEach((servicegroup: ServiceHealthEntry[], service: string) => {
            Results.set(service, new Map<string, Map<string, number>>());

            let regionGrouping = this.groupBy(servicegroup, entry => entry.Region);

            regionGrouping.forEach((regiongroup: ServiceHealthEntry[], region: string) => {
                Results.get(service).set(region, new Map<string, number>());

                for (let item of regiongroup) {
                    // The item.MonthlyOutageDurations may not be considered a map depending on the output format. The csv output
                    // is explicitly parsing the string to JSON, so it should be a map, but the JSON parsing is treating the input
                    // as an object. Thus to be safe, treat the property as an object and iterate the keys, then get the values

                    Object.keys(item.MonthlyOutageDurations).forEach(yearMonth => {
                        let newNum = item.MonthlyOutageDurations[yearMonth];

                        if (Results.get(service).get(region).has(yearMonth)) {
                            newNum += Results.get(service).get(region).get(yearMonth);
                        }

                        Results.get(service).get(region).set(yearMonth, newNum);
                    });
                }

                Results.get(service).get(region).forEach((outage: number, yearMonth: string) => {
                    let alert = new Alert();
                    alert.service = service;
                    alert.region = region;
                    let parts: string[] = yearMonth.split("-");
                    // The + unary operator converts string to int
                    alert.year = +parts[0];
                    alert.month = +parts[1];
                    alert.totalDownTime = outage;

                    let temp: Date = new Date(alert.year, alert.month);
                    // Using 0 for the day provides the last day of the month
                    // Days * Hours * Minutes * Seconds
                    let totalSecondsInMonth: number = (new Date(temp.getUTCFullYear(), temp.getUTCMonth(), 0).getUTCDate()) * 24 * 60 * 60

                    alert.availability = (1 - (alert.totalDownTime / totalSecondsInMonth)) * 100;

                    alerts.push(alert);
                });
            });
        });

        // The sort will go from smallest to largest, and we want to display it
        // from the most recent date down.
        this.alertDataSource.data = alerts.sort((a: Alert, b: Alert) => {
            if (a.year < b.year) {
                return -1;
            }
            else if (a.year > b.year) {
                return 1;
            }
            else if (a.month > b.month) {
                return 1;
            }
            else if (a.month < b.month) {
                return -1;
            }
            else {
                return 0;
            }
        }).reverse();

        /*    .sort((a: Alert, b: Alert) => {
            if (a.service < b.service) {
                return -1;
            }
            else if (a.service > b.service) {
                return 1;
            }
            else {
                return 0;
            }
        }).sort((a: Alert, b: Alert) => {
            if (a.region < b.region) {
                return -1;
            }
            else if (a.region > b.region) {
                return 1;
            }
            else {
                return 0;
            }
        });*/
    }

    public downloadAlerts() {
        //IE can use the msSaveBlob function
        let ieEDGE = navigator.userAgent.match(/Edge/g);
        let ie = navigator.userAgent.match(/.NET/g); // IE 11+
        let oldIE = navigator.userAgent.match(/MSIE/g);

        let keys = Object.keys(this.alertDataSource.data[0]);
        const lineDelimiter = "\n";
        const itemDelimiter = ",";
        let result: string = "";

        result += keys.join(itemDelimiter);
        result += lineDelimiter;

        this.alertDataSource.data.forEach(function (item) {
            let columnIndex: number = 0;

            keys.forEach(function (key) {
                result += item[key];

                if (columnIndex < keys.length - 1) {
                    result += itemDelimiter;
                }
            });
            result += lineDelimiter;
        });

        let blob = new Blob([result], { type: "data:text/csv;charset=utf-8" });
        let fileName: string = "serviceavailability_alerts.csv"

        if (ie || oldIE || ieEDGE) {
            window.navigator.msSaveBlob(blob, fileName);
        }
        else {

            let link = document.createElement('a');
            let url = window.URL.createObjectURL(blob);
            link.href = url
            link.download = fileName;
            link.click();

            setTimeout(function () {
                window.URL.revokeObjectURL(url);
            }, 0);
        }
    }

    private groupBy(list, keyGetter): Map<string, any> {
        const map = new Map();
        list.forEach((item) => {
            const key = keyGetter(item);
            const collection = map.get(key);

            if (!collection) {
                map.set(key, [item]);
            }
            else {
                collection.push(item);
            }
        });

        return map;
    }
}
