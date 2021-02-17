import { Component, OnInit, Inject, ViewChild } from '@angular/core';

import { AwsService } from '../services/aws-service';
import { IRegionService } from '../services/region.service';
import { IAwsServiceService } from '../services/aws-service.service';
import { AwsRegion } from '../services/region';
import { Request } from './request';
import { NgForm } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { DialogContentComponent } from '../services/dialog.component';
import { ServiceHealthEntry } from './servicehealthentry';
import { Papa } from 'ngx-papaparse';
import { AlertComponent } from '../alert/alert.component';
import { ResultComponent } from '../result/result.component';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';









@Component({
  selector: "app-request",
  styleUrls: [
    './request.component.css'
  ],
  templateUrl: './request.component.html'
})
export class RequestComponent implements OnInit {

  selectedIndex: number = 0;

  title: string = "AWS Service Availability";

  services: AwsService[] = [];

  regions: AwsRegion[] = [];

  outputs: string[] = [
    "None",
    "json",
    "csv"
  ];

  @ViewChild(AlertComponent) alerts: AlertComponent;
  @ViewChild(ResultComponent) results: ResultComponent;

  constructor(@Inject("IRegionService") private _regionService: IRegionService,
    @Inject("IAwsServiceService") private _awsserviceService: IAwsServiceService,
    public request: Request,
    public dialog: MatDialog,
    private papa: Papa
  ) {
  }

  ngOnInit() {
    this._regionService.getAllRegions().then(response => {
      this.regions = [{ name: "All", code: "All" }].concat(response);
    });

    this._awsserviceService.getAwsServices().then(response => {
      this.services = [{ name: "All", value: "All" }].concat(response);
    });
  }

  onTabChange($event: any) {
    this.selectedIndex = $event.index;
  }

  openHelp() {
    let dialogRef = this.dialog.open(DialogContentComponent, {
      width: '600px',
      data: {
        keyvalues: [
          {
            key: "Service",
            value: 'Select "All" or select no options to query all services. Optionally, select specific services to query against.'
          },
          {
            key: "Start",
            value: "Select a date to query for events after. Leave this field blank to not limit how far back the query will go."
          },
          {
            key: "End",
            value: "Select a date to query for events before. Leave this field blank to not limit how recently events occured."
          },
          {
            key: "Output",
            value: 'The output can either be just diplayed in the data tab or downloaded as a csv or json file (and will also be displayed).' +
              ' You will be prompted to save the file once it is generated if you selected csv or json.'
          },
          {
            key: "Submit",
            value: "Press the submit button when you are ready to execute the query."
          }
        ],
        title: "Help"
      },
      disableClose: false
    });
  }

  onSubmit(form: NgForm) {
    let valid: Boolean = true;

    if (this.request.start !== undefined && this.request.start !== null &&
      this.request.end !== undefined && this.request.end !== null) {
      if (this.request.end < this.request.start) {
        valid = false;
        let dialogRef = this.dialog.open(DialogContentComponent, {
          height: '200px',
          width: '400px',
          data: {
            message: "The requested end date cannot be before the start date.",
            title: "ERROR",
            buttons: [
              {
                text: "Ok",
                type: "button",
                result: "ok"
              }
            ]
          },
          disableClose: false
        });
      }
    }

    if (valid === true) {

      let dialogRef = this.dialog.open(DialogContentComponent, {
        height: '200px',
        width: '400px',
        data: {
          message: "Getting the data.",
          title: "Waiting..."
        },
        disableClose: true
      });

      //The regions and services properties get assigned to as string arrays by the html page, however, when the
      //object is passed to Submit, the class has them defined as strings, so it automatically calls join which
      //converts them into a comma delimited list
      let observable: Observable<HttpResponse<string>> = this.request.Submit();

      let results: ServiceHealthEntry[];

      observable.subscribe(response => {

        switch (this.request.output) {
          default:
          case "json": {
            results = JSON.parse(response.body);
            break;
          }
          case "csv": {
            this.papa.parse(response.body, {
              header: true,
              dynamicTyping: true,
              complete: (res, file) => {
                results = res.data;
                results.map((val: ServiceHealthEntry) => {
                  // The CSV parser is interpreting the monthly outage durations as a string, but
                  // it's really JSON
                  // Replace any double quotes the csv parser may have produced, not sure why it does that, but as of 10/4/17
                  // it makes just the last monthly outage durations object in the data parse as a string to {\"\"YYYY-mm\"\":12233}
                  val.MonthlyOutageDurations = JSON.parse(val.MonthlyOutageDurations.toString().replace(new RegExp("\"\"", 'g'), "\""));
                });
              }
            });
            break;
          }
        }

        //Set up the data source to populate the table
        this.results.buildResults(results);

        // Populate the data for the alerts tab
        this.alerts.buildAlerts(results);

        //This will switch to the data tab
        this.selectedIndex = 1;

        //Close the modal dialog now that the response has been delivered
        dialogRef.close();

        //If the user requested a format, then prompt for download
        if (this.request.output === "json" || this.request.output === "csv") {
          //IE can use the msSaveBlob function
          let ieEDGE = navigator.userAgent.match(/Edge/g);
          let ie = navigator.userAgent.match(/.NET/g); // IE 11+
          let oldIE = navigator.userAgent.match(/MSIE/g);

          let blob = new Blob([response.body], { type: (this.request.output === "json" ? "application/octet-stream" : "data:text/csv;charset=utf-8") });
          let fileName: string = "serviceavailability." + this.request.output;

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

      },
        error => {
          console.log(error);
          dialogRef.close();
        });
    }
  }
}
