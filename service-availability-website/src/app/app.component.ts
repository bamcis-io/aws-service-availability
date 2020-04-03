import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from './../environments/environment';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  providers: [
  ]
})
export class AppComponent implements OnInit {
  title = 'AWS Service Availability';
  configLoaded: boolean = false;
  appConfigPath = "/appConfig.json";

  constructor(private http: HttpClient) { }

  ngOnInit(): void {
    this.loadRuntimeConfig();
  }

  private loadRuntimeConfig() {
    this.http.get(this.appConfigPath).subscribe((response: JSON) => {
      try {
        environment.url = response["url"];
      }
      catch (error) {
        console.warn(error);
      }

      this.configLoaded = true;
    },
    error => {   
      console.warn(error);
      console.log("CAUGHT ERROR");
      this.configLoaded = true;
    });
  }
}
