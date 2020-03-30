
import { fromEvent as observableFromEvent } from 'rxjs';

import {distinctUntilChanged, debounceTime} from 'rxjs/operators';
import { Component, OnInit, ViewChild, AfterViewInit, ElementRef } from '@angular/core';

import { MatSort } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';
import { ServiceHealthEntry } from '../request/servicehealthentry';
import { MatTableDataSource } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';

import { DialogContentComponent } from '../services/dialog.component';

import { merge as observableMerge } from 'rxjs';

import { map } from 'rxjs/operators';






@Component({
    selector: "app-result",
    styleUrls: [
        './result.component.css'
    ],
    templateUrl: './result.component.html'
})
export class ResultComponent implements OnInit, AfterViewInit {

    displayedColumns: string[] = [
        "Index",
        "Service",
        "Region",
        "Date",
        "Began",
        "Ended",
        "ElapsedTime",
        "Summary"
    ];

    public resultDataSource = new MatTableDataSource<ServiceHealthEntry>();

    @ViewChild(MatSort) sort: MatSort;
    @ViewChild(MatPaginator) paginator: MatPaginator;
    @ViewChild('filter') filter: ElementRef;

    constructor(public dialog: MatDialog) {
    }

    ngOnInit() {
    }

    ngAfterViewInit() {
        this.setupDataSource();
    }

    public openResultDescription(text: string) {
        let dialogRef = this.dialog.open(DialogContentComponent, {
            width: '400px',
            data: {
                title: "Details",
                message: text,
                buttons: [
                    {
                        text: "Ok",
                        type: "button",
                        result: "ok"
                    }
                ]
            }
        });
    }

    public buildResults(results: ServiceHealthEntry[]) {
        this.resultDataSource.data = results;
    }

    private setupDataSource() {
        this.resultDataSource.paginator = this.paginator;
        this.resultDataSource.sort = this.sort;
        this.resultDataSource.filterPredicate = (data: ServiceHealthEntry, filter: string) => {
            return data.Service.indexOf(filter) !== -1;
        };

        observableFromEvent(this.filter.nativeElement, 'keyup').pipe(
            debounceTime(150),
            distinctUntilChanged())
            .subscribe(() => {
                if (!this.resultDataSource) {
                    return;
                }
                this.resultDataSource.filter = this.filter.nativeElement.value;
                this.resultDataSource.paginator.firstPage();
            });

        // reset paginator after sorting
        this.sort.sortChange.subscribe(() => this.paginator.pageIndex = 0);

        let displayDataChanges: any = [
            this.resultDataSource.paginator.page,
            this.resultDataSource.sort.sortChange
        ];

        observableMerge(...displayDataChanges).pipe(map(() => {

            if (this.resultDataSource.data !== undefined && this.resultDataSource.data !== null) {
                let startIndex = this.resultDataSource.paginator.pageIndex * this.resultDataSource.paginator.pageSize;
                let pageSize = this.resultDataSource.paginator.pageSize;

                let temp: ServiceHealthEntry[] = this.getSortedData(this.resultDataSource.data);

                temp = this.resultDataSource.data.filter((item: ServiceHealthEntry) => {
                    let searchStr = item.Service;
                    return searchStr.indexOf(this.resultDataSource.filter) !== -1;
                });
   
                temp = temp.splice(startIndex, pageSize);

                return temp;
            }
            else {
                return new Array<ServiceHealthEntry>();
            }
        })).subscribe();
    }

    private getSortedData(data: ServiceHealthEntry[]): ServiceHealthEntry[] {

        if (this.sort === undefined || !this.sort.active || this.sort.direction == '') {
            return data;
        }
        else {
            return data.sort((a, b) => {
                let propertyA: number | string = '';
                let propertyB: number | string = '';

                switch (this.sort.active) {
                    case "Service": {
                        [propertyA, propertyB] = [a.Service, b.Service];
                        break;
                    }
                    case "Region": {
                        [propertyA, propertyB] = [a.Region, b.Region];
                        break;
                    }
                    case "Date": {
                        [propertyA, propertyB] = [a.Date, b.Date];
                        break;
                    }
                    case "Began": {
                        [propertyA, propertyB] = [a.Began, b.Began];
                        break;
                    }
                    case "Ended": {
                        [propertyA, propertyB] = [a.Ended, b.Ended];
                        break;
                    }
                    case "ElapsedTime": {
                        [propertyA, propertyB] = [a.ElapsedTime, b.ElapsedTime];
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

}
