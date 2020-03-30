
import { merge as observableMerge, BehaviorSubject } from 'rxjs';
import { Observable } from 'rxjs/observable';

import { map } from 'rxjs/operators';
import { DataSource } from '@angular/cdk/table';
import { Alert } from './alert';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';


// MatTableDataSource makes this unnecessary now
export class AlertDataSource extends DataSource<any> {
    private _dataChange: BehaviorSubject<Alert[]> = new BehaviorSubject<Alert[]>([]);

    _filterChange = new BehaviorSubject('');
    get filter(): string {
        return this._filterChange.value;
    }
    set filter(filter: string) {
        this._filterChange.next(filter);
    }

    get results(): Alert[] {
        return this._results;
    }
    set results(data: Alert[]) {
        this._results = data;
        this._dataChange.next(data);
    }


    public paginator: MatPaginator;
    public sort: MatSort;

    constructor(private _results: Alert[]) {
        super();
    }

    private _length: number = 0;

    public get length(): number {
        return this._length;
    }

    // This is called on instantiation and any emitted event
    connect(): Observable<Alert[]> {

        const displayDataChanges: any = [
            this._dataChange,
            this._filterChange
        ];

        if (this.paginator !== undefined) {
            displayDataChanges.push(this.paginator.page);
        }

        if (this.sort !== undefined) {
            displayDataChanges.push(this.sort.sortChange);
        }

        return observableMerge(...displayDataChanges).pipe(map(() => {

            if (this._results !== undefined && this._results !== null) {
                let startIndex = this.paginator !== undefined ? this.paginator.pageIndex * this.paginator.pageSize : 0;
                let pageSize = this.paginator !== undefined ? this.paginator.pageSize : 25;

                let temp: Alert[] = this.getSortedData(this._results);

                temp = temp.filter((item: Alert) => {
                    let searchStr = item.service;
                    return searchStr.indexOf(this.filter) !== -1;
                });

                this._length = temp.length;

                temp = temp.splice(startIndex, pageSize);

                return temp;
            }
            else {
                return new Array<Alert>();
            }
        }));
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

    disconnect() {
    }
}
