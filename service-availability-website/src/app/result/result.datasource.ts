
import { merge as observableMerge, BehaviorSubject, InteropObservable } from 'rxjs';
import { Observable } from 'rxjs/observable';

import { map } from 'rxjs/operators';
import { DataSource } from '@angular/cdk/table';
import { ServiceHealthEntry } from '../request/servicehealthentry';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';


// MatTableDataSource makes this unnecessary now
export class ResultDataSource extends DataSource<any> {
    private _dataChange: BehaviorSubject<ServiceHealthEntry[]> = new BehaviorSubject<ServiceHealthEntry[]>([]);

    _filterChange = new BehaviorSubject('');
    get filter(): string {
        return this._filterChange.value;
    }
    set filter(filter: string) {
        this._filterChange.next(filter);
    }

    set results(data: ServiceHealthEntry[]) {
        this._results = data;
        this._dataChange.next(data);
    }

    public paginator: MatPaginator;
    public sort: MatSort;

    constructor(private _results: ServiceHealthEntry[]) {
        super();
    }

    private _length: number = 0;

    public get length(): number {
        return this._length;
    }

    // This is called on instantiation and any emitted event
    connect(): Observable<ServiceHealthEntry[]> {

        let displayDataChanges: any = [
            this._dataChange,
            this._filterChange,
            this.paginator.page,
            this.sort.sortChange
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

                let temp: ServiceHealthEntry[] = this.getSortedData(this._results);

                temp = this._results.filter((item: ServiceHealthEntry) => {
                    let searchStr = item.Service;
                    return searchStr.indexOf(this.filter) !== -1;
                });

                this._length = temp.length;

                temp = temp.splice(startIndex, pageSize);

                return temp;
            }
            else {
                return new Array<ServiceHealthEntry>();
            }
        }));
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

    disconnect() {
    }
}
