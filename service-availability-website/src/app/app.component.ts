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
  configLoaded: boolean = false;
  appConfigPath = "/appConfig.json";

  constructor(private http: HttpClient) {
  }

  ngOnInit(): void {
    this.loadRuntimeConfig();
  }

  private loadRuntimeConfig() {
    this.http.get(this.appConfigPath).subscribe(
      (response: JSON) => {
        try {
          environment.url = response["url"];
          environment.identityPool = response["identityPool"];
          environment.oidc.authority = response["oidc"]["authority"];
          environment.oidc.client_id = response["oidc"]["client_id"];
          environment.oidc.redirect_uri = response["oidc"]["redirect_uri"];
          environment.oidc.logout_redirect_uri = response["oidc"]["logout_redirect_uri"];
        }
        catch (error) {
          console.warn(error);
        }

        this.configLoaded = true;
      },
      error => {
        console.warn(error);
        console.warn("CAUGHT ERROR DURIG APPCONFIG.JSON LOAD");
        this.configLoaded = true;
      }
    );
  }
}
